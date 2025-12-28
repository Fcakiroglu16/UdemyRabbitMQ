using System.Text;
using RabbitMQ.Client;

namespace DeadLetterExample;

/// <summary>
/// Dead Letter Exchange (DLX) senaryolar? için Producer s?n?f?.
/// Ana kuyruk, Dead Letter Exchange ve Dead Letter Queue yap?land?rmas? yapar.
/// </summary>
public class DeadLetterProducer
{
    private readonly IConnection _connection;
    private IChannel? _channel;

    // Exchange ve Queue isimleri
    private const string MainExchange = "main-exchange";
    private const string MainQueue = "main-queue";
    private const string MainRoutingKey = "order.created";

    private const string DeadLetterExchange = "dead-letter-exchange";
    private const string DeadLetterQueue = "dead-letter-queue";
    private const string DeadLetterRoutingKey = "failed";

    public DeadLetterProducer(IConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Dead Letter Exchange altyap?s?n? kurar:
    /// 1. Main Exchange ve Queue
    /// 2. Dead Letter Exchange ve Queue
    /// 3. Gerekli binding'leri yapar
    /// </summary>
    public async Task SetupInfrastructureAsync()
    {
        _channel = await _connection.CreateChannelAsync();

        Console.WriteLine("?? Dead Letter Exchange Altyap?s? Kuruluyor...");
        Console.WriteLine("???????????????????????????????????????????????????????\n");

        // 1. Dead Letter Exchange olu?tur (önce bu olmal?)
        await _channel.ExchangeDeclareAsync(
            exchange: DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false
        );
        Console.WriteLine($"? Dead Letter Exchange olu?turuldu: {DeadLetterExchange}");

        // 2. Dead Letter Queue olu?tur
        await _channel.QueueDeclareAsync(
            queue: DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );
        Console.WriteLine($"? Dead Letter Queue olu?turuldu: {DeadLetterQueue}");

        // 3. DLQ'yu DLX'e ba?la
        await _channel.QueueBindAsync(
            queue: DeadLetterQueue,
            exchange: DeadLetterExchange,
            routingKey: DeadLetterRoutingKey
        );
        Console.WriteLine($"? DLQ, DLX'e ba?land? (Routing Key: {DeadLetterRoutingKey})");

        // 4. Main Exchange olu?tur
        await _channel.ExchangeDeclareAsync(
            exchange: MainExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false
        );
        Console.WriteLine($"? Main Exchange olu?turuldu: {MainExchange}");

        // 5. Main Queue olu?tur - DLX yap?land?rmas? ile
        var mainQueueArguments = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", DeadLetterExchange },
            { "x-dead-letter-routing-key", DeadLetterRoutingKey },
            { "x-message-ttl", 30000 } // 30 saniye TTL (test için)
        };

        await _channel.QueueDeclareAsync(
            queue: MainQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArguments
        );
        Console.WriteLine($"? Main Queue olu?turuldu: {MainQueue}");
        Console.WriteLine($"  ? Dead Letter Exchange: {DeadLetterExchange}");
        Console.WriteLine($"  ? Dead Letter Routing Key: {DeadLetterRoutingKey}");
        Console.WriteLine($"  ? Message TTL: 30 saniye");

        // 6. Main Queue'yu Main Exchange'e ba?la
        await _channel.QueueBindAsync(
            queue: MainQueue,
            exchange: MainExchange,
            routingKey: MainRoutingKey
        );
        Console.WriteLine($"? Main Queue, Main Exchange'e ba?land? (Routing Key: {MainRoutingKey})");

        Console.WriteLine("\n? Dead Letter Exchange altyap?s? ba?ar?yla kuruldu!\n");
    }

    /// <summary>
    /// Main Queue'ya mesaj gönderir.
    /// Bu mesajlar i?lenmezse veya reject edilirse DLQ'ya dü?ecektir.
    /// </summary>
    public async Task PublishMessageAsync(string message, bool persistent = true)
    {
        if (_channel == null)
        {
            throw new InvalidOperationException("Infrastructure not setup. Call SetupInfrastructureAsync first.");
        }

        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = persistent,
            ContentType = "text/plain",
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            MessageId = Guid.NewGuid().ToString()
        };

        await _channel.BasicPublishAsync(
            exchange: MainExchange,
            routingKey: MainRoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body
        );

        Console.WriteLine($"?? Mesaj gönderildi: {message}");
        Console.WriteLine($"   ? Exchange: {MainExchange}");
        Console.WriteLine($"   ? Routing Key: {MainRoutingKey}");
        Console.WriteLine($"   ? Message ID: {properties.MessageId}");
    }

    /// <summary>
    /// Birden fazla test mesaj? gönderir
    /// </summary>
    public async Task PublishMultipleMessagesAsync(int count)
    {
        Console.WriteLine($"\n?? {count} adet test mesaj? gönderiliyor...\n");

        for (int i = 1; i <= count; i++)
        {
            var message = $"Test Mesaj? #{i} - {DateTime.Now:HH:mm:ss}";
            await PublishMessageAsync(message);
            await Task.Delay(100); // Küçük bir gecikme
        }

        Console.WriteLine($"\n? {count} mesaj ba?ar?yla gönderildi!\n");
    }

    /// <summary>
    /// TTL süresi dolacak mesajlar gönderir (DLQ'ya dü?ecek)
    /// </summary>
    public async Task PublishExpirableMessagesAsync(int count)
    {
        Console.WriteLine($"\n? {count} adet süreli mesaj gönderiliyor (30 saniye TTL)...");
        Console.WriteLine("Bu mesajlar tüketilmezse 30 saniye sonra DLQ'ya dü?ecek!\n");

        for (int i = 1; i <= count; i++)
        {
            var message = $"Süreli Mesaj #{i} - Tüketilmezse DLQ'ya dü?ecek";
            await PublishMessageAsync(message);
        }

        Console.WriteLine($"\n? {count} süreli mesaj gönderildi!");
        Console.WriteLine("?? ?pucu: Consumer'? çal??t?rmazsan?z 30 saniye sonra DLQ'da görebilirsiniz.\n");
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
