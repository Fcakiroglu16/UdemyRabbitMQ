#region

using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

#endregion

namespace Producer;

/// <summary>
///     RabbitMQ kuyruklarına mesaj gönderme işlemlerini içeren sınıf.
///     Her metod farklı bir kuyruk tipine mesaj gönderme senaryosunu gösterir.
/// </summary>
public class RabbitMQMessagePublisher(IChannel channel)
{
    #region Lazy Queue Mesaj Gönderme

    /// <summary>
    ///     Lazy queue'ya mesaj gönderme
    /// </summary>
    public async Task SendToLazyQueue(string message = "Hello World - Lazy Queue")
    {
        var queueName = "lazy-queue";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true // Lazy queue'da persistent önerilir
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   💾 Mesaj disk'te saklanacak");
    }

    #endregion

    #region Single Active Consumer Mesaj Gönderme

    /// <summary>
    ///     Single active consumer kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToSingleActiveConsumerQueue(string message = "Hello World - Single Active Consumer")
    {
        var queueName = "single-active-consumer-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   👤 Sadece aktif consumer tarafından işlenecek");
    }

    #endregion

    #region Karşılaştırma Kuyrukları Mesaj Gönderme

    /// <summary>
    ///     Karşılaştırma için tüm kuyruk tiplerine mesaj gönderme
    /// </summary>
    public async Task SendToAllComparisonQueues()
    {
        // Classic Queue
        var body = Encoding.UTF8.GetBytes("Hello World - Compare Classic");
        await channel.BasicPublishAsync(string.Empty, "compare-classic", body);
        Console.WriteLine("📤 Mesaj gönderildi -> compare-classic");

        // Quorum Queue
        body = Encoding.UTF8.GetBytes("Hello World - Compare Quorum");
        var properties = new BasicProperties { Persistent = true };
        await channel.BasicPublishAsync(string.Empty, "compare-quorum", false, properties, body);
        Console.WriteLine("📤 Mesaj gönderildi -> compare-quorum");

        // Stream Queue
        body = Encoding.UTF8.GetBytes("Hello World - Compare Stream");
        await channel.BasicPublishAsync(string.Empty, "compare-stream", body);
        Console.WriteLine("📤 Mesaj gönderildi -> compare-stream");

        Console.WriteLine("\n✅ Tüm karşılaştırma kuyruklarına mesaj gönderildi");
    }

    #endregion

    #region Temel Kuyruk Mesaj Gönderme

    /// <summary>
    ///     Basit geçici kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToBasicTemporaryQueue(string message = "Hello World - Basic Temp Queue")
    {
        var queueName = "basic-temp-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
    }

    /// <summary>
    ///     Kalıcı kuyruğa kalıcı mesaj gönderme
    ///     - Mesajlar RabbitMQ yeniden başlatıldığında korunur
    /// </summary>
    public async Task SendToDurableQueue(string message = "Hello World - Durable Queue")
    {
        var queueName = "durable-queue";
        var body = Encoding.UTF8.GetBytes(message);

        // Kalıcı mesaj için properties ayarla
        var properties = new BasicProperties
        {
            Persistent = true // Mesajı kalıcı yap
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Kalıcı mesaj gönderildi -> {queueName}: {message}");
    }

    /// <summary>
    ///     Exclusive kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToExclusiveQueue(string message = "Hello World - Exclusive Queue")
    {
        var queueName = "exclusive-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
    }

    /// <summary>
    ///     Auto-delete kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToAutoDeleteQueue(string message = "Hello World - Auto Delete Queue")
    {
        var queueName = "auto-delete-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
    }

    /// <summary>
    ///     Sunucu tarafından isimlendirilen kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToServerNamedQueue(string queueName, string message = "Hello World - Server Named Queue")
    {
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
    }

    #endregion

    #region TTL Kuyruk Mesaj Gönderme

    /// <summary>
    ///     Mesaj TTL'li kuyruğa mesaj gönderme
    ///     - Mesajlar 60 saniye sonra otomatik silinir
    /// </summary>
    public async Task SendToMessageTTLQueue(string message = "Hello World - Message TTL Queue")
    {
        var queueName = "message-ttl-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   ⏱️  Mesaj 60 saniye sonra otomatik silinecek");
    }

    /// <summary>
    ///     Kuyruk TTL'li kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToQueueTTLQueue(string message = "Hello World - Queue TTL Queue")
    {
        var queueName = "queue-ttl-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
    }

    /// <summary>
    ///     Mesaj başına özel TTL ile mesaj gönderme
    ///     - Kuyruktaki TTL'den bağımsız, mesaj bazlı TTL
    /// </summary>
    public async Task SendWithCustomMessageTTL(string queueName, string message, int ttlMilliseconds)
    {
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Expiration = ttlMilliseconds.ToString() // Mesaj bazlı TTL
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Özel TTL'li mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine($"   ⏱️  TTL: {ttlMilliseconds}ms");
    }

    #endregion

    #region Dead Letter Exchange Mesaj Gönderme

    /// <summary>
    ///     DLX'li kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToDeadLetterExchangeQueue(string message = "Hello World - DLX Queue")
    {
        var queueName = "main-queue-with-dlx";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   💀 30 saniye sonra DLX'e gönderilecek");
    }

    /// <summary>
    ///     Dead Letter Queue sistemine mesaj gönderme
    /// </summary>
    public async Task SendToDeadLetterQueueSystem(string message = "Hello World - DLQ System")
    {
        var queueName = "main-processing-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   💀 Başarısız olursa DLQ'ya yönlendirilecek");
    }

    #endregion

    #region Max Length Kuyruk Mesaj Gönderme

    /// <summary>
    ///     Maksimum mesaj sayılı kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToMaxLengthQueue(string message = "Hello World - Max Length Queue")
    {
        var queueName = "max-length-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
    }

    /// <summary>
    ///     Maksimum byte boyutlu kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToMaxLengthBytesQueue(string message = "Hello World - Max Length Bytes Queue")
    {
        var queueName = "max-length-bytes-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
    }

    /// <summary>
    ///     Overflow davranışlı kuyruğa mesaj gönderme
    /// </summary>
    public async Task SendToOverflowQueue(string message = "Hello World - Overflow Queue")
    {
        var queueName = "overflow-reject-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   ⚠️  100 mesaj limitinde yeni mesajlar reddedilir");
    }

    #endregion

    #region Priority Queue Mesaj Gönderme

    /// <summary>
    ///     Öncelikli kuyruğa öncelik belirterek mesaj gönderme
    /// </summary>
    public async Task SendToPriorityQueue(string message, byte priority = 5)
    {
        var queueName = "priority-queue";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Priority = priority // 0-10 arası öncelik
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Öncelikli mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine($"   ⭐ Öncelik: {priority}/10");
    }

    /// <summary>
    ///     Farklı önceliklerle birden fazla mesaj gönderme
    /// </summary>
    public async Task SendMultiplePriorityMessages()
    {
        await SendToPriorityQueue("Düşük öncelikli mesaj", 1);
        await SendToPriorityQueue("Normal öncelikli mesaj");
        await SendToPriorityQueue("Yüksek öncelikli mesaj", 10);
        await SendToPriorityQueue("Kritik mesaj", 10);
    }

    #endregion

    #region Quorum Queue Mesaj Gönderme

    /// <summary>
    ///     Quorum queue'ya mesaj gönderme
    /// </summary>
    public async Task SendToQuorumQueue(string message = "Hello World - Quorum Queue")
    {
        var queueName = "quorum-queue";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true // Quorum queue'da persistent zorunlu
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   🔒 Yüksek güvenilirlikle saklanacak");
    }

    /// <summary>
    ///     Delivery limit'li quorum queue'ya mesaj gönderme
    /// </summary>
    public async Task SendToQuorumQueueWithDeliveryLimit(string message = "Hello World - Quorum DL Queue")
    {
        var queueName = "quorum-queue-with-delivery-limit";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   🔄 Maksimum 3 kez redeliver edilebilir");
    }

    #endregion

    #region Stream Queue Mesaj Gönderme

    /// <summary>
    ///     Stream queue'ya mesaj gönderme
    /// </summary>
    public async Task SendToStreamQueue(string message = "Hello World - Stream Queue")
    {
        var queueName = "stream-queue";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   🌊 Mesaj stream'e kalıcı olarak eklendi");
    }

    /// <summary>
    ///     Retention policy'li stream queue'ya mesaj gönderme
    /// </summary>
    public async Task SendToStreamQueueWithRetention(string message = "Hello World - Stream Queue with Retention")
    {
        var queueName = "stream-queue-with-retention";
        var body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   ⏰ Mesaj 7 gün boyunca saklanacak");
    }

    #endregion

    #region Kombine Senaryolar Mesaj Gönderme

    /// <summary>
    ///     Üretim ortamı kuyruğuna öncelikli ve kalıcı mesaj gönderme
    /// </summary>
    public async Task SendToProductionReadyQueue(string message, byte priority = 5)
    {
        var queueName = "production-ready-queue";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            Priority = priority,
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Üretim mesajı gönderildi -> {queueName}: {message}");
        Console.WriteLine($"   🏭 Öncelik: {priority}, MessageId: {properties.MessageId}");
    }

    /// <summary>
    ///     Retry sistemine mesaj gönderme
    /// </summary>
    public async Task SendToRetryQueueSystem(string message = "Hello World - Retry System")
    {
        var queueName = "main-retry-queue";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = Guid.NewGuid().ToString()
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine("   🔄 Başarısız olursa retry mekanizması devreye girecek");
    }

    /// <summary>
    ///     Classic queue'ya mesaj gönderme
    /// </summary>
    public async Task SendToClassicQueue(string message = "Hello World - Classic Queue")
    {
        var queueName = "classic-queue";
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi -> {queueName}: {message}");
    }

    #endregion

    #region Toplu Mesaj Gönderme

    /// <summary>
    ///     Tüm oluşturulan kuyruklara "Hello World" mesajı gönderme
    /// </summary>
    public async Task SendToAllQueues()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          Tüm Kuyruklara Mesaj Gönderiliyor...              ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        try
        {
            // Temel kuyruklar
            await SendToBasicTemporaryQueue();
            await SendToDurableQueue();
            await SendToExclusiveQueue();
            await SendToAutoDeleteQueue();

            // TTL kuyruklar
            await SendToMessageTTLQueue();
            await SendToQueueTTLQueue();

            // DLX kuyruklar
            await SendToDeadLetterExchangeQueue();
            await SendToDeadLetterQueueSystem();

            // Max Length kuyruklar
            await SendToMaxLengthQueue();
            await SendToMaxLengthBytesQueue();
            await SendToOverflowQueue();

            // Priority queue
            await SendToPriorityQueue("Hello World - Priority Queue (Öncelik 7)", 7);

            // Lazy queue
            await SendToLazyQueue();

            // Single active consumer
            await SendToSingleActiveConsumerQueue();

            // Quorum queues
            await SendToQuorumQueue();
            await SendToQuorumQueueWithDeliveryLimit();

            // Stream queues
            await SendToStreamQueue();
            await SendToStreamQueueWithRetention();

            // Kombine senaryolar
            await SendToProductionReadyQueue("Hello World - Production Queue", 8);
            await SendToRetryQueueSystem();
            await SendToClassicQueue();

            // Karşılaştırma kuyrukları
            await SendToAllComparisonQueues();

            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║       ✅ Tüm Kuyruklara Mesaj Gönderimi Tamamlandı!       ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Hata oluştu: {ex.Message}");
        }
    }

    /// <summary>
    ///     Belirli bir kuyruğa birden fazla mesaj gönderme
    /// </summary>
    public async Task SendMultipleMessages(string queueName, int count, bool persistent = true)
    {
        Console.WriteLine($"\n📦 {queueName} kuyruğuna {count} mesaj gönderiliyor...\n");

        for (var i = 1; i <= count; i++)
        {
            var message = $"Hello World - Message #{i}";
            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = persistent,
                MessageId = Guid.NewGuid().ToString()
            };

            await channel.BasicPublishAsync(
                string.Empty,
                queueName,
                false,
                properties,
                body
            );

            if (i % 10 == 0 || i == count) Console.WriteLine($"   ✓ {i}/{count} mesaj gönderildi");
        }

        Console.WriteLine($"\n✅ Toplam {count} mesaj başarıyla gönderildi!");
    }

    #endregion

    #region Özel Mesaj Gönderme Senaryoları

    /// <summary>
    ///     Custom headers ile mesaj gönderme
    /// </summary>
    public async Task SendMessageWithHeaders(string queueName, string message, Dictionary<string, object> headers)
    {
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Headers = headers,
            Persistent = true,
            ContentType = "text/plain",
            ContentEncoding = "UTF-8"
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Header'lı mesaj gönderildi -> {queueName}: {message}");
        Console.WriteLine($"   📋 Headers: {string.Join(", ", headers.Select(h => $"{h.Key}={h.Value}"))}");
    }

    /// <summary>
    ///     JSON mesaj gönderme örneği
    /// </summary>
    public async Task SendJsonMessage(string queueName, object messageObject)
    {
        var jsonMessage = JsonSerializer.Serialize(messageObject);
        var body = Encoding.UTF8.GetBytes(jsonMessage);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };

        await channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 JSON mesajı gönderildi -> {queueName}");
        Console.WriteLine($"   📄 {jsonMessage}");
    }

    #endregion
}