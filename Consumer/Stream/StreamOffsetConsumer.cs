#region

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

#endregion

namespace Consumer.Stream;

/// <summary>
///     RabbitMQ Stream'de BELİRLİ OFFSET ARALIĞINDAKİ mesajları okuma örnekleri.
///     Stream'ler Kafka benzeri "offset" kavramı kullanır:
///     - Her mesaj, stream içinde 0'dan başlayan benzersiz bir offset numarasına sahiptir.
///     - "x-stream-offset" consumer argümanı ile okumaya hangi offset'ten başlanacağı belirlenir.
///     - Mesajlar silinmez; aynı aralık tekrar tekrar okunabilir (replay).
///     Desteklenen offset değerleri: "first", "last", "next", DateTimeOffset (timestamp) veya offset numarası (long).
/// </summary>
public class StreamOffsetConsumer
{
    private readonly IConnection _connection;

    public StreamOffsetConsumer(IConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    ///     Belirli bir offset ARALIĞINDAKİ mesajları okur: [startOffset, endOffset] (her iki uç dahil).
    ///     - Okumaya startOffset'ten başlanır ("x-stream-offset" = startOffset).
    ///     - Mesajın offset'i endOffset'i aştığında tüketim durdurulur.
    ///     - autoAck = false kullanılır; stream manuel ack için QoS (prefetch) zorunludur.
    /// </summary>
    /// <param name="streamName">Stream queue adı.</param>
    /// <param name="startOffset">Okumaya başlanacak offset (dahil).</param>
    /// <param name="endOffset">Okumanın biteceği offset (dahil).</param>
    public async Task ConsumeByOffsetRangeAsync(string streamName, long startOffset, long endOffset)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  STREAM CONSUME - OFFSET ARALIĞI OKUMA");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        if (startOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(startOffset), "Offset negatif olamaz.");

        if (endOffset < startOffset)
            throw new ArgumentException("endOffset, startOffset'ten küçük olamaz.", nameof(endOffset));

        await using var channel = await _connection.CreateChannelAsync();
        await DeclareStreamAsync(channel, streamName);

        // Stream'de manuel ack için QoS zorunlu
        await channel.BasicQosAsync(0, 50, false);

        var consumedCount = 0;
        var finished = false;

        // Okumaya başlama offset'i: doğrudan offset numarası verilir.
        var consumerArgs = new Dictionary<string, object>
        {
            { "x-stream-offset", startOffset }
        };

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var messageOffset = StreamAckConsumer.GetStreamOffset(ea);

            // Aralık dışına çıktıysak (offset > endOffset) mesajı onaylayıp dur.
            if (messageOffset > endOffset)
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false);
                finished = true;
                return;
            }

            var message = Encoding.UTF8.GetString(ea.Body.ToArray());

            Console.WriteLine($"📥 Offset {messageOffset}: {message}");
            Console.WriteLine($"   MessageId: {ea.BasicProperties.MessageId}");

            await channel.BasicAckAsync(ea.DeliveryTag, false);
            consumedCount++;

            // endOffset'e ulaştıysak aralık tamamlandı.
            if (messageOffset >= endOffset)
                finished = true;
        };

        var consumerTag = await channel.BasicConsumeAsync(
            streamName,
            false, // autoAck = false (manuel ack)
            string.Empty,
            false,
            false,
            consumerArgs,
            consumer,
            CancellationToken.None
        );

        Console.WriteLine($"🎧 Stream offset aralığı okunuyor: {streamName}");
        Console.WriteLine($"   Aralık: [{startOffset} - {endOffset}] (her iki uç dahil)\n");

        await WaitUntilAsync(() => finished);

        await channel.BasicCancelAsync(consumerTag);

        Console.WriteLine($"\n✅ Offset aralığında {consumedCount} mesaj okundu");
        Console.WriteLine("   🔄 Mesajlar silinmedi, aynı aralık tekrar okunabilir (replay)\n");
    }

    /// <summary>
    ///     Belirli bir offset'ten başlayarak EN FAZLA "count" adet mesaj okur.
    ///     - "x-stream-offset" = startOffset ile başlanır.
    ///     - count adet mesaj okunduğunda veya timeout dolduğunda durur.
    ///     - autoAck = false; QoS (prefetch) zorunludur.
    /// </summary>
    /// <param name="streamName">Stream queue adı.</param>
    /// <param name="startOffset">Okumaya başlanacak offset (dahil).</param>
    /// <param name="count">Okunacak maksimum mesaj sayısı.</param>
    public async Task ConsumeFromOffsetAsync(string streamName, long startOffset, int count)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  STREAM CONSUME - OFFSET'TEN İTİBAREN N MESAJ");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        if (startOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(startOffset), "Offset negatif olamaz.");

        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Mesaj sayısı pozitif olmalıdır.");

        await using var channel = await _connection.CreateChannelAsync();
        await DeclareStreamAsync(channel, streamName);

        await channel.BasicQosAsync(0, (ushort)Math.Min(count, ushort.MaxValue), false);

        var consumedCount = 0;

        var consumerArgs = new Dictionary<string, object>
        {
            { "x-stream-offset", startOffset }
        };

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            // Hedef sayıya ulaşıldıysa fazladan gelen mesajı onaylayıp atla.
            if (consumedCount >= count)
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false);
                return;
            }

            var message = Encoding.UTF8.GetString(ea.Body.ToArray());
            var messageOffset = StreamAckConsumer.GetStreamOffset(ea);

            Console.WriteLine($"📥 Offset {messageOffset}: {message}");

            await channel.BasicAckAsync(ea.DeliveryTag, false);
            consumedCount++;
        };

        var consumerTag = await channel.BasicConsumeAsync(
            streamName,
            false,
            string.Empty,
            false,
            false,
            consumerArgs,
            consumer,
            CancellationToken.None
        );

        Console.WriteLine($"🎧 Stream offset'ten okunuyor: {streamName}");
        Console.WriteLine($"   Başlangıç offset: {startOffset}");
        Console.WriteLine($"   Okunacak mesaj sayısı: {count}\n");

        await WaitUntilAsync(() => consumedCount >= count);

        await channel.BasicCancelAsync(consumerTag);

        Console.WriteLine($"\n✅ Offset {startOffset}'ten itibaren {consumedCount} mesaj okundu\n");
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
    ///     Verilen koşul sağlanana kadar veya timeout süresi dolana kadar bekler.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (!condition() && DateTime.UtcNow - startTime < timeout)
            await Task.Delay(100);
    }
}
