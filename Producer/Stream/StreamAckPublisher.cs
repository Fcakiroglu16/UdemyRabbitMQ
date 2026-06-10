#region

using RabbitMQ.Client;
using System.Text;

#endregion

namespace Producer.Stream;

/// <summary>
///     RabbitMQ Stream'e Publisher Confirms (ACK) AÇIK ve KAPALI olarak mesaj gönderimi.
///     Stream: append-only, yüksek throughput, replay edilebilir mesaj yapısı (Kafka benzeri).
///     - ACK KAPALI: Fire-and-forget, en hızlı, broker onayı beklenmez (teslim garantisi yok)
///     - ACK AÇIK:   Publisher confirms, broker onayı beklenir (güvenilir gönderim)
///     Not: Stream queue tek bir kez declare edilir; tekrar declare etmek idempotent'tir.
/// </summary>
public class StreamAckPublisher
{
    private readonly IConnection _connection;

    public StreamAckPublisher(IConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    ///     Metod 1: Publisher Confirms KAPALI - Fire and Forget
    ///     - Channel publisher confirms DEVRE DIŞI oluşturulur (publisherConfirmationsEnabled: false)
    ///     - Mesaj stream'e gönderilir, broker onayı beklenmez
    ///     - En yüksek throughput / en düşük gecikme
    ///     - Mesajın stream'e yazıldığı GARANTİ EDİLMEZ
    ///     - Kullanım: Kritik olmayan, kaybı kabul edilebilir mesajlar (log, metric, telemetry)
    /// </summary>
    public async Task PublishWithoutAckAsync(string streamName, IEnumerable<string> messages)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  STREAM PUBLISH - ACK KAPALI (Fire and Forget)");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        // Publisher confirms KAPALI channel
        // CreateChannelOptions(publisherConfirmationsEnabled, publisherConfirmationTrackingEnabled, rateLimiter)
        var channelOpts = new CreateChannelOptions(
            false, // publisherConfirmationsEnabled: false → onay yok
            false, // publisherConfirmationTrackingEnabled: false → takip yok
            new ThrottlingRateLimiter(1000)
        );

        await using var channel = await _connection.CreateChannelAsync(channelOpts);

        await DeclareStreamAsync(channel, streamName);

        var sentCount = 0;

        foreach (var message in messages)
        {
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Mesaj gönder - onay BEKLENMEZ (fire and forget)
            await channel.BasicPublishAsync(
                string.Empty, // default exchange
                streamName, // routing key = stream adı
                false, // mandatory: false
                properties,
                body
            );

            sentCount++;
            Console.WriteLine($"📤 Gönderildi (ACK yok): {message}");
            Console.WriteLine($"   MessageId: {properties.MessageId}");
        }

        Console.WriteLine($"\n   ⚡ Toplam {sentCount} mesaj fire-and-forget olarak gönderildi");
        Console.WriteLine("   ⚠️  Broker onayı beklenmedi, teslim garantisi yok\n");
    }

    /// <summary>
    ///     Metod 2: Publisher Confirms AÇIK - Güvenli Gönderim
    ///     - Channel publisher confirms AKTİF oluşturulur (publisherConfirmationsEnabled + tracking: true)
    ///     - BasicPublishAsync, broker onayı (ack) gelene kadar TAMAMLANMAZ → await = confirmed
    ///     - Mesajın stream'e yazıldığı GARANTİ EDİLİR
    ///     - Biraz daha yavaş ama güvenilir
    ///     - Kullanım: Kritik mesajlar (order, payment, event sourcing)
    /// </summary>
    public async Task PublishWithAckAsync(string streamName, IEnumerable<string> messages)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  STREAM PUBLISH - ACK AÇIK (Publisher Confirms)");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        // Publisher confirms AÇIK channel
        var channelOpts = new CreateChannelOptions(
            true, // publisherConfirmationsEnabled: true → onay aktif
            true, // publisherConfirmationTrackingEnabled: true → onay takibi aktif
            new ThrottlingRateLimiter(1000)
        );

        await using var channel = await _connection.CreateChannelAsync(channelOpts);

        await DeclareStreamAsync(channel, streamName);

        var confirmedCount = 0;

        foreach (var message in messages)
        {
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Mesaj gönder - publisher confirms aktif olduğu için
            // await tamamlandığında broker mesajı onaylamış olur.
            await channel.BasicPublishAsync(
                string.Empty, // default exchange
                streamName, // routing key = stream adı
                true, // mandatory: true → yönlendirilemezse hata
                properties,
                body
            );

            confirmedCount++;
            Console.WriteLine($"📤 Gönderildi ve ONAYLANDI: {message}");
            Console.WriteLine($"   MessageId: {properties.MessageId}");
            Console.WriteLine("   ✅ Broker onayı (ack) alındı");
        }

        Console.WriteLine($"\n   🔒 Toplam {confirmedCount} mesaj broker tarafından onaylandı");
        Console.WriteLine("   ✓ Güvenli gönderim (mesajlar stream'e yazıldı)\n");
    }

    /// <summary>
    ///     Stream queue'yu tanımlar (idempotent).
    ///     x-queue-type: stream → append-only, replay edilebilir queue türü.
    /// </summary>
    private static async Task DeclareStreamAsync(IChannel channel, string streamName)
    {
        var arguments = new Dictionary<string, object>
        {
            { "x-queue-type", "stream" }, // Stream queue türü
            { "x-max-length-bytes", 20_000_000 }, // 20MB retention (toplam boyut sınırı)
            { "x-stream-max-segment-size-bytes", 500_000 } // 500KB segment boyutu
        };

        await channel.QueueDeclareAsync(
            streamName,
            true, // durable: stream queue her zaman durable'dır
            false, // exclusive: stream queue exclusive olamaz
            false, // autoDelete: stream queue autoDelete olamaz
            arguments
        );
    }
}
