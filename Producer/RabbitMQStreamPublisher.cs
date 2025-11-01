#region

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

#endregion

namespace Producer;

/// <summary>
///     RabbitMQ Stream ile mesaj gönderme ve tüketme işlemleri.
///     Stream: Yüksek performanslı, append-only log benzeri mesaj yapısı.
///     - Mesajlar silinmez, replay edilebilir
///     - Yüksek throughput (milyonlarca mesaj/saniye)
///     - Kafka benzeri davranış
/// </summary>
public class RabbitMQStreamPublisher
{
    private readonly IChannel _channel;

    public RabbitMQStreamPublisher(IConnection connection)
    {
        var channelOpts = new CreateChannelOptions(
            true,
            true,
            new ThrottlingRateLimiter(1000) // Stream için daha yüksek limit
        );
        _channel = connection.CreateChannelAsync(channelOpts).Result;
    }

    /// <summary>
    ///     Stream'e mesaj gönderme
    ///     - Stream oluşturulur (yoksa)
    ///     - Mesajlar append-only şekilde eklenir
    ///     - Mesajlar silinmez, retention policy'e göre saklanır
    /// </summary>
    public async Task PublishToStream(string streamName, string message)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  STREAM PUBLISH - Mesaj Gönderimi");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        try
        {
            // Stream queue tanımlama (x-queue-type: stream)
            var arguments = new Dictionary<string, object>
            {
                { "x-queue-type", "stream" },
                { "x-max-length-bytes", 20_000_000 }, // 20MB retention
                { "x-stream-max-segment-size-bytes", 500_000 } // 500KB segment
            };

            await _channel.QueueDeclareAsync(
                streamName,
                true,
                false,
                false,
                arguments
            );

            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Stream'e mesaj gönder
            await _channel.BasicPublishAsync(
                string.Empty,
                streamName,
                true,
                properties,
                body
            );

            Console.WriteLine($"📤 Stream'e mesaj gönderildi: {message}");
            Console.WriteLine($"   Stream: {streamName}");
            Console.WriteLine($"   MessageId: {properties.MessageId}");
            Console.WriteLine($"   Timestamp: {properties.Timestamp}");
            Console.WriteLine("   ✅ Mesaj stream'e eklendi (append-only)");
            Console.WriteLine("   🔄 Mesaj replay edilebilir\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}\n");
            throw;
        }
    }

    /// <summary>
    ///     Stream'den mesaj tüketme (consume)
    ///     - Offset-based consumption (Kafka benzeri)
    ///     - Mesajlar silinmez, tekrar okunabilir
    ///     - "first" offset: Stream başından başla
    ///     - "last" offset: Son mesajdan başla
    ///     - "next" offset: Yeni mesajları bekle
    /// </summary>
    public async Task ConsumeFromStream(string streamName, int messageCount = 10, string offset = "first")
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  STREAM CONSUME - Mesaj Tüketimi");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        try
        {
            var consumedCount = 0;

            // Stream consumer arguments
            var consumerArgs = new Dictionary<string, object>
            {
                { "x-stream-offset", offset } // "first", "last", "next" veya timestamp
            };

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"📥 Mesaj alındı: {message}");
                Console.WriteLine($"   MessageId: {ea.BasicProperties.MessageId}");
                Console.WriteLine($"   Timestamp: {ea.BasicProperties.Timestamp}");
                Console.WriteLine($"   DeliveryTag: {ea.DeliveryTag}");
                Console.WriteLine($"   Offset: {offset}\n");

                // Stream'de manual ack gerekli
                await _channel.BasicAckAsync(ea.DeliveryTag, false);

                consumedCount++;
                await Task.CompletedTask;
            };

            // Consumer başlat
            var consumerTag = await _channel.BasicConsumeAsync(
                streamName,
                false, // Manual ack
                string.Empty, // Let RabbitMQ generate the tag
                true,
                false,
                consumerArgs,
                consumer,
                CancellationToken.None
            );

            Console.WriteLine($"🎧 Stream dinleniyor: {streamName}");
            Console.WriteLine($"   Offset: {offset}");
            Console.WriteLine($"   ConsumerTag: {consumerTag}");
            Console.WriteLine($"   Hedef mesaj sayısı: {messageCount}\n");

            // Belirli sayıda mesaj gelene kadar bekle veya timeout
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (consumedCount < messageCount && DateTime.UtcNow - startTime < timeout) await Task.Delay(100);

            // Consumer'ı durdur
            await _channel.BasicCancelAsync(consumerTag);

            Console.WriteLine($"✅ Toplam {consumedCount} mesaj tüketildi");
            Console.WriteLine("   🔄 Stream mesajları silinmedi, tekrar okunabilir\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}\n");
            throw;
        }
    }
}