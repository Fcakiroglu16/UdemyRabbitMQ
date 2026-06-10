#region

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

#endregion

namespace Consumer.Stream;

/// <summary>
///     RabbitMQ Stream (x-queue-type: stream) tipi queue dinleme örneği.
///     Tek metod ile ACK KAPALI (otomatik onay) ve ACK AÇIK (manuel onay) senaryoları.
///     Stream mesajları silinmez; "x-stream-offset" ile baştan tekrar okunabilir.
/// </summary>
public class StreamAckConsumer
{
    private readonly IConnection _connection;

    public StreamAckConsumer(IConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    ///     Stream (x-queue-type: stream) tipi queue'dan mesaj okur.
    ///     Tek metod ile hem ACK KAPALI hem ACK AÇIK senaryosu:
    ///     - manualAck = false → autoAck = true (mesaj teslim edilince otomatik onaylanır)
    ///     - manualAck = true  → autoAck = false (mesaj BasicAckAsync ile onaylanır, QoS zorunlu)
    /// </summary>
    public async Task ConsumeAsync(
        string streamName,
        bool manualAck,
        int messageCount = 5,
        string offset = "first")
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine(manualAck
            ? "  STREAM CONSUME - ACK AÇIK (manuel onay)"
            : "  STREAM CONSUME - ACK KAPALI (otomatik onay)");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        await using var channel = await _connection.CreateChannelAsync();
        await DeclareStreamAsync(channel, streamName);

        // Manuel ACK için QoS (prefetch) zorunludur.
        if (manualAck)
            await channel.BasicQosAsync(0, 10, false);

        var consumedCount = 0;

        // Stream'de okumaya hangi offset'ten başlanacağı: "first" / "last" / "next" / offset no
        var consumerArgs = new Dictionary<string, object>
        {
            { "x-stream-offset", offset }
        };

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            Console.WriteLine($"📥 Offset {GetStreamOffset(ea)}: {message}");

            // Manuel onay açıksa mesaj işlendikten sonra onaylanır.
            if (manualAck)
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false);
                Console.WriteLine("   ✅ Manuel ACK gönderildi");
            }

            consumedCount++;
        };

        var consumerTag = await channel.BasicConsumeAsync(
            streamName,
            !manualAck, // autoAck: manuel onay yoksa otomatik onay
            string.Empty,
            false,
            false,
            consumerArgs,
            consumer,
            CancellationToken.None
        );

        Console.WriteLine($"🎧 Stream dinleniyor: {streamName} (offset: {offset})\n");

        // Hedef mesaj sayısına ulaşana kadar veya 30 sn timeout olana kadar bekle.
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;
        while (consumedCount < messageCount && DateTime.UtcNow - startTime < timeout)
            await Task.Delay(100);

        await channel.BasicCancelAsync(consumerTag);

        Console.WriteLine($"\n✅ Toplam {consumedCount} mesaj okundu");
        Console.WriteLine("   🔄 Stream mesajları silinmedi, tekrar okunabilir\n");
    }

    /// <summary>
    ///     Stream queue'yu tanımlar (idempotent). Producer ile aynı argümanlar kullanılmalıdır.
    /// </summary>
    private static async Task DeclareStreamAsync(IChannel channel, string streamName)
    {
        var arguments = new Dictionary<string, object>
        {
            { "x-queue-type", "stream" },
            { "x-max-length-bytes", 20_000_000 },
            { "x-stream-max-segment-size-bytes", 500_000 }
        };

        await channel.QueueDeclareAsync(
            streamName,
            true,
            false,
            false,
            arguments
        );
    }

    /// <summary>
    ///     Teslim edilen stream mesajının offset numarasını "x-stream-offset" header'ından okur.
    ///     Header bulunamazsa -1 döner.
    /// </summary>
    internal static long GetStreamOffset(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is { } headers
            && headers.TryGetValue("x-stream-offset", out var value)
            && value is long offset)
            return offset;

        return -1;
    }
}
