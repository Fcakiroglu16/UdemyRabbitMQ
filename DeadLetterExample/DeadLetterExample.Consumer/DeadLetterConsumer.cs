using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DeadLetterExample;

/// <summary>
/// Dead Letter Exchange senaryolar? için Consumer s?n?f?.
/// Ana kuyruktan mesaj tüketir ve farkl? senaryolarda reject eder.
/// </summary>
public class DeadLetterConsumer
{
    private readonly IConnection _connection;
    private IChannel? _channel;

    private const string MainQueue = "main-queue";
    private const string DeadLetterQueue = "dead-letter-queue";

    public DeadLetterConsumer(IConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Ana kuyruktan mesaj tüketir - Ba?ar?l? i?leme senaryosu
    /// Tüm mesajlar ba?ar?yla i?lenir ve acknowledge edilir
    /// </summary>
    public async Task ConsumeMainQueueAsync(CancellationToken cancellationToken = default)
    {
        _channel = await _connection.CreateChannelAsync();
        await _channel.BasicQosAsync(0, 1, false); // Prefetch count = 1

        Console.WriteLine("?? Main Queue'dan mesaj tüketiliyor...");
        Console.WriteLine($"?? Queue: {MainQueue}");
        Console.WriteLine("? Mod: Tüm mesajlar ba?ar?yla i?lenecek\n");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        var processedCount = 0;

        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            processedCount++;

            Console.WriteLine($"?? Mesaj al?nd? #{processedCount}:");
            Console.WriteLine($"   ? ?çerik: {message}");
            Console.WriteLine($"   ? Message ID: {eventArgs.BasicProperties.MessageId}");
            Console.WriteLine($"   ? Delivery Tag: {eventArgs.DeliveryTag}");

            // Mesaj? ba?ar?yla i?le
            await Task.Delay(500); // Simüle i?lem süresi

            // ACK gönder
            await _channel.BasicAckAsync(eventArgs.DeliveryTag, false);
            Console.WriteLine($"   ? Mesaj ba?ar?yla i?lendi ve ACK gönderildi\n");
        };

        await _channel.BasicConsumeAsync(
            queue: MainQueue,
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("? Mesajlar bekleniyor... (Durdurmak için Ctrl+C)\n");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\n?? Consumer durduruldu. Toplam {processedCount} mesaj i?lendi.");
        }
    }

    /// <summary>
    /// Ana kuyruktan mesaj tüketir - Hata senaryosu
    /// Mesajlar? reject eder, böylece DLQ'ya dü?erler
    /// </summary>
    public async Task ConsumeAndRejectMessagesAsync(CancellationToken cancellationToken = default)
    {
        _channel = await _connection.CreateChannelAsync();
        await _channel.BasicQosAsync(0, 1, false);

        Console.WriteLine("? Main Queue'dan mesaj tüketiliyor (REJECT modu)...");
        Console.WriteLine($"?? Queue: {MainQueue}");
        Console.WriteLine("??  Mod: Tüm mesajlar reject edilecek ve DLQ'ya dü?ecek\n");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        var rejectedCount = 0;

        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            rejectedCount++;

            Console.WriteLine($"?? Mesaj al?nd? #{rejectedCount}:");
            Console.WriteLine($"   ? ?çerik: {message}");
            Console.WriteLine($"   ? Message ID: {eventArgs.BasicProperties.MessageId}");

            // Simüle hata durumu
            await Task.Delay(200);

            // NACK gönder (requeue = false, DLQ'ya gönder)
            await _channel.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: false // DLQ'ya gönder
            );

            Console.WriteLine($"   ? Mesaj REJECT edildi ve DLQ'ya gönderildi\n");
        };

        await _channel.BasicConsumeAsync(
            queue: MainQueue,
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("? Mesajlar bekleniyor... (Durdurmak için Ctrl+C)\n");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\n?? Consumer durduruldu. Toplam {rejectedCount} mesaj reject edildi.");
        }
    }

    /// <summary>
    /// Ana kuyruktan mesaj tüketir - Karma senaryo
    /// Baz? mesajlar? ba?ar?yla i?ler, baz?lar?n? reject eder
    /// </summary>
    public async Task ConsumeWithMixedResultsAsync(int successRate = 50, CancellationToken cancellationToken = default)
    {
        _channel = await _connection.CreateChannelAsync();
        await _channel.BasicQosAsync(0, 1, false);

        Console.WriteLine("?? Main Queue'dan mesaj tüketiliyor (KARMA mod)...");
        Console.WriteLine($"?? Queue: {MainQueue}");
        Console.WriteLine($"??  Mod: ~%{successRate} ba?ar? oran? (rastgele)\n");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        var processedCount = 0;
        var successCount = 0;
        var rejectedCount = 0;
        var random = new Random();

        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            processedCount++;

            Console.WriteLine($"?? Mesaj al?nd? #{processedCount}:");
            Console.WriteLine($"   ? ?çerik: {message}");
            Console.WriteLine($"   ? Message ID: {eventArgs.BasicProperties.MessageId}");

            await Task.Delay(300);

            // Rastgele ba?ar?/hata
            var isSuccess = random.Next(100) < successRate;

            if (isSuccess)
            {
                await _channel.BasicAckAsync(eventArgs.DeliveryTag, false);
                successCount++;
                Console.WriteLine($"   ? Mesaj ba?ar?yla i?lendi (Toplam ba?ar?l?: {successCount})\n");
            }
            else
            {
                await _channel.BasicNackAsync(eventArgs.DeliveryTag, false, false);
                rejectedCount++;
                Console.WriteLine($"   ? Mesaj reject edildi ve DLQ'ya gönderildi (Toplam reject: {rejectedCount})\n");
            }
        };

        await _channel.BasicConsumeAsync(
            queue: MainQueue,
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("? Mesajlar bekleniyor... (Durdurmak için Ctrl+C)\n");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\n?? Consumer durduruldu.");
            Console.WriteLine($"?? ?statistikler:");
            Console.WriteLine($"   ? Toplam i?lenen: {processedCount}");
            Console.WriteLine($"   ? Ba?ar?l?: {successCount}");
            Console.WriteLine($"   ? Reject: {rejectedCount}");
        }
    }

    /// <summary>
    /// Dead Letter Queue'dan mesaj tüketir
    /// DLQ'daki mesajlar? analiz etmek veya i?lemek için kullan?l?r
    /// </summary>
    public async Task ConsumeDeadLetterQueueAsync(CancellationToken cancellationToken = default)
    {
        _channel = await _connection.CreateChannelAsync();
        await _channel.BasicQosAsync(0, 1, false);

        Console.WriteLine("?? Dead Letter Queue'dan mesaj tüketiliyor...");
        Console.WriteLine($"?? Queue: {DeadLetterQueue}");
        Console.WriteLine("?? Bu mesajlar ana kuyruktan reject edilenlerdir\n");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        var dlqCount = 0;

        consumer.ReceivedAsync += async (sender, eventArgs) =>
        {
            var body = eventArgs.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            dlqCount++;

            Console.WriteLine($"?? DLQ Mesaj? #{dlqCount}:");
            Console.WriteLine($"   ? ?çerik: {message}");
            Console.WriteLine($"   ? Message ID: {eventArgs.BasicProperties.MessageId}");
            Console.WriteLine($"   ? Original Timestamp: {eventArgs.BasicProperties.Timestamp}");

            // DLQ header'lar?n? kontrol et
            if (eventArgs.BasicProperties.Headers != null)
            {
                Console.WriteLine("   ? DLQ Headers:");
                foreach (var header in eventArgs.BasicProperties.Headers)
                {
                    var value = header.Value;
                    if (value is byte[] bytes)
                    {
                        value = Encoding.UTF8.GetString(bytes);
                    }
                    Console.WriteLine($"      • {header.Key}: {value}");
                }
            }

            // DLQ'dan da ACK gönder
            await _channel.BasicAckAsync(eventArgs.DeliveryTag, false);
            Console.WriteLine($"   ? DLQ mesaj? i?lendi ve ACK gönderildi\n");
        };

        await _channel.BasicConsumeAsync(
            queue: DeadLetterQueue,
            autoAck: false,
            consumer: consumer
        );

        Console.WriteLine("? DLQ mesajlar? bekleniyor... (Durdurmak için Ctrl+C)\n");

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"\n?? DLQ Consumer durduruldu. Toplam {dlqCount} mesaj i?lendi.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }
    }
}
