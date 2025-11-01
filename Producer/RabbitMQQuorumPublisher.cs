#region

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

#endregion

namespace Producer;

/// <summary>
///     RabbitMQ Quorum Queue ile mesaj gönderme ve tüketme işlemleri.
///     Quorum Queue: Yüksek erişilebilirlik ve veri güvenliği için tasarlanmış queue türü.
///     - Raft consensus algoritması kullanır
///     - Mesajlar çoğunluk (quorum) replikasyonu ile saklanır
///     - Node çökmeleri karşısında dayanıklıdır
///     - Publisher confirms ve consumer acknowledgements zorunludur
///     - Yüksek güvenilirlik gerektiren sistemler için idealdir
/// </summary>
public class RabbitMQQuorumPublisher
{
    private readonly IChannel _channel;

    public RabbitMQQuorumPublisher(IConnection connection)
    {
        var channelOpts = new CreateChannelOptions(
            true,
            true,
            new ThrottlingRateLimiter(500)
        );
        _channel = connection.CreateChannelAsync(channelOpts).Result;
    }

    /// <summary>
    ///     Quorum Queue'ya mesaj gönderme
    ///     - Queue oluşturulur (yoksa) - Quorum queue türünde
    ///     - Mesajlar Raft protokolü ile replika edilir
    ///     - Çoğunluk onayı (quorum) alındıktan sonra mesaj kabul edilir
    ///     - Publisher confirms otomatik aktiftir
    /// </summary>
    public async Task PublishToQuorum(string queueName, string message)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  QUORUM QUEUE PUBLISH - Mesaj Gönderimi");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        try
        {
            // Quorum Queue tanımlama (x-queue-type: quorum)
            // Quorum Queue Özellikleri:
            // - x-queue-type: "quorum" → Raft consensus algoritması
            // - x-quorum-initial-group-size: Replika sayısı (varsayılan: 3)
            //   * Minimum: 1 (tek node - HA yok)
            //   * Önerilen: 3 (1 leader + 2 follower)
            //   * Maksimum: 5-7 (daha fazla node'da performans düşer)
            //   * Quorum hesaplama: (replica_count / 2) + 1
            //     Örnek: 3 replica → quorum = 2 (en az 2 node ayakta olmalı)
            //     Örnek: 5 replica → quorum = 3 (en az 3 node ayakta olmalı)
            // - x-max-length: Maksimum mesaj sayısı (overflow politikası: drop-head)
            // - x-max-in-memory-length: Bellekte tutulacak maksimum mesaj sayısı
            // - x-delivery-limit: Mesaj kaç kez redelivery yapılabilir (poison message koruması)
            var arguments = new Dictionary<string, object>
            {
                { "x-queue-type", "quorum" }, // Quorum queue türü
                //{ "x-quorum-initial-group-size", 1 }, // 3 replika (1 leader + 2 follower)
                { "x-max-length", 10_000 }, // Maksimum 10.000 mesaj
                { "x-max-in-memory-length", 1_000 }, // Bellekte maksimum 1.000 mesaj
                { "x-delivery-limit", 5 } // Maksimum 5 redelivery (sonra dead letter)
            };

            // Quorum queue declare
            // QueueDeclareAsync parametreleri:
            // 1. queue: Queue adı
            // 2. durable: true - Quorum queue'lar her zaman durable'dır
            // 3. exclusive: false - Quorum queue'lar exclusive olamaz
            // 4. autoDelete: false - Quorum queue'lar autoDelete olamaz
            // 5. arguments: Quorum queue argümanları
            await _channel.QueueDeclareAsync(
                queueName,
                true, // Quorum queue için durable zorunlu
                false, // Quorum queue exclusive olamaz
                false, // Quorum queue autoDelete olamaz
                arguments
            );


            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true, // Mesaj kalıcı (quorum queue için önemli)
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                ContentType = "text/plain",
                DeliveryMode = DeliveryModes.Persistent // Persistent delivery mode
            };

            // Quorum queue'ya mesaj gönder
            await _channel.BasicPublishAsync(
                string.Empty, // Default exchange
                queueName, // Routing key = queue name
                true, // mandatory: true - Queue yoksa hata fırlat
                properties,
                body
            );

            // Publisher confirm bekle
            // WaitForConfirmsOrDieAsync: Tüm mesajların onaylanmasını bekler
            // Quorum'dan onay gelene kadar bloklar


            Console.WriteLine($"📤 Quorum Queue'ya mesaj gönderildi: {message}");
            Console.WriteLine($"   Queue: {queueName}");
            Console.WriteLine($"   MessageId: {properties.MessageId}");
            Console.WriteLine($"   Timestamp: {properties.Timestamp}");
            Console.WriteLine("   Type: Quorum (Raft Consensus)");
            Console.WriteLine("   ✅ Mesaj quorum tarafından onaylandı (replicated)");
            Console.WriteLine("   🔒 Yüksek güvenilirlik garantisi\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}\n");
            throw;
        }
    }

    /// <summary>
    ///     Quorum Queue'dan mesaj tüketme (consume)
    ///     - Manuel acknowledgement zorunludur
    ///     - Mesaj çoğunluk (quorum) onayı ile silinir
    ///     - Redelivery limit kontrolü (x-delivery-limit)
    ///     - At-least-once delivery garantisi
    /// </summary>
    public async Task ConsumeFromQuorum(string queueName, int messageCount = 10)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  QUORUM QUEUE CONSUME - Mesaj Tüketimi");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        try
        {
            var consumedCount = 0;

            // Quorum queue tanımlama (idempotent)
            var arguments = new Dictionary<string, object>
            {
                { "x-queue-type", "quorum" },
                { "x-max-length", 10_000 },
                { "x-max-in-memory-length", 1_000 },
                { "x-delivery-limit", 5 }
            };

            await _channel.QueueDeclareAsync(
                queueName,
                true,
                false,
                false,
                arguments
            );

            // QoS ayarı - Prefetch count
            // Quorum queue'larda prefetch count önemlidir
            // BasicQosAsync parametreleri:
            // 1. prefetchSize: 0 (kullanılmaz)
            // 2. prefetchCount: Aynı anda işlenecek mesaj sayısı
            // 3. global: false (channel bazlı)
            await _channel.BasicQosAsync(0, 1, false);

            Console.WriteLine("🎧 Quorum Queue dinleniyor");
            Console.WriteLine($"   Queue: {queueName}");
            Console.WriteLine("   Type: Quorum (Raft Consensus)");
            Console.WriteLine($"   Hedef mesaj sayısı: {messageCount}");
            Console.WriteLine("   Prefetch Count: 1\n");

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // Redelivery count kontrolü
                var redelivered = ea.Redelivered;
                var deliveryTag = ea.DeliveryTag;

                Console.WriteLine($"📥 Mesaj alındı: {message}");
                Console.WriteLine($"   Queue: {queueName}");
                Console.WriteLine($"   MessageId: {ea.BasicProperties.MessageId}");
                Console.WriteLine($"   Timestamp: {ea.BasicProperties.Timestamp}");
                Console.WriteLine($"   DeliveryTag: {deliveryTag}");
                Console.WriteLine($"   Redelivered: {redelivered}");

                // x-death header'ını kontrol et (redelivery count)
                if (ea.BasicProperties.Headers?.ContainsKey("x-death") == true)
                    Console.WriteLine("   ⚠️  Mesaj daha önce redeliver edildi");

                Console.WriteLine();

                try
                {
                    // Mesaj işleme simülasyonu
                    await Task.Delay(100);

                    // Manuel ACK - Quorum queue'da zorunlu
                    // Mesaj quorum tarafından onaylanır ve silinir
                    await _channel.BasicAckAsync(deliveryTag, false);

                    Console.WriteLine("   ✅ Mesaj onaylandı (ACK) - Quorum'dan silindi\n");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Mesaj işleme hatası: {ex.Message}");

                    // NACK - Mesaj tekrar queue'ya döner
                    // BasicNackAsync parametreleri:
                    // 1. deliveryTag: Mesaj ID
                    // 2. multiple: false (sadece bu mesaj)
                    // 3. requeue: true (tekrar queue'ya ekle)
                    await _channel.BasicNackAsync(deliveryTag, false, true);

                    Console.WriteLine("   🔄 Mesaj redeliver edilecek (NACK + requeue)\n");
                }

                consumedCount++;
                await Task.CompletedTask;
            };

            // Consumer başlat
            // Quorum queue'da autoAck = false zorunludur
            var consumerTag = await _channel.BasicConsumeAsync(
                queueName,
                false, // autoAck = false (manuel ack zorunlu)
                string.Empty,
                false,
                false,
                null,
                consumer,
                CancellationToken.None
            );

            Console.WriteLine($"   ConsumerTag: {consumerTag}");
            Console.WriteLine("   ⏳ Mesajlar bekleniyor...\n");

            // Belirli sayıda mesaj gelene kadar bekle veya timeout
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (consumedCount < messageCount && DateTime.UtcNow - startTime < timeout) await Task.Delay(100);

            // Consumer'ı durdur
            await _channel.BasicCancelAsync(consumerTag);

            Console.WriteLine($"✅ Toplam {consumedCount} mesaj tüketildi");
            Console.WriteLine("   🔒 Quorum queue yüksek güvenilirlik garantisi");
            Console.WriteLine("   ♻️  At-least-once delivery\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}\n");
            throw;
        }
    }
}