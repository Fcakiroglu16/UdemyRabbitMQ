#region

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

#endregion

namespace Producer;

/// <summary>
///     RabbitMQ Fanout Exchange ile mesaj gönderme ve tüketme işlemleri.
///     Fanout Exchange: Bağlı olan TÜM queue'lara mesaj gönderir (broadcast).
///     - Routing key yok sayılır
///     - Pub/Sub pattern için idealdir
///     - Log broadcasting, notification sistemi gibi senaryolar için uygundur
/// </summary>
public class RabbitMQFanoutPublisher
{
    private readonly IChannel _channel;

    public RabbitMQFanoutPublisher(IConnection connection)
    {
        var channelOpts = new CreateChannelOptions(
            true,
            true,
            new ThrottlingRateLimiter(500)
        );
        _channel = connection.CreateChannelAsync(channelOpts).Result;
    }

    /// <summary>
    ///     Fanout Exchange'e mesaj gönderme
    ///     - Exchange'e gönderilen mesaj TÜM bağlı queue'lara iletilir
    ///     - Routing key kullanılmaz (boş string gönderilir)
    ///     - Broadcasting pattern (1 mesaj → N queue)
    /// </summary>
    public async Task PublishToFanout(string exchangeName, string message)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  FANOUT EXCHANGE - Mesaj Gönderimi (Broadcast)");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        try
        {
            // Fanout Exchange tanımlama
            // ExchangeDeclareAsync parametreleri:
            // 1. exchange: Exchange adı
            // 2. type: "fanout" - Tüm bağlı queue'lara broadcast
            // 3. durable: true - Exchange restart sonrası kalıcı
            // 4. autoDelete: false - Son queue ayrılınca exchange silinmesin
            await _channel.ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Fanout,
                true,
                false
            );

            var body = Encoding.UTF8.GetBytes(message);

            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                ContentType = "text/plain"
            };

            // Fanout exchange'e mesaj gönder
            // Routing key boş string ("") - Fanout'ta routing key kullanılmaz
            await _channel.BasicPublishAsync(
                exchangeName,
                string.Empty, // Fanout'ta routing key önemsiz
                false,
                properties,
                body
            );

            Console.WriteLine($"📤 Fanout Exchange'e mesaj gönderildi: {message}");
            Console.WriteLine($"   Exchange: {exchangeName}");
            Console.WriteLine("   Type: Fanout (Broadcast)");
            Console.WriteLine($"   MessageId: {properties.MessageId}");
            Console.WriteLine($"   Timestamp: {properties.Timestamp}");
            Console.WriteLine("   ✅ Mesaj TÜM bağlı queue'lara iletilecek");
            Console.WriteLine("   📢 Broadcasting pattern aktif\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}\n");
            throw;
        }
    }

    /// <summary>
    ///     Fanout Exchange'den mesaj tüketme
    ///     - Her consumer kendi queue'sunu oluşturur
    ///     - Queue exchange'e bind edilir (routing key gerekmez)
    ///     - Aynı mesaj TÜM consumer'lara iletilir
    /// </summary>
    public async Task ConsumeFromFanout(string exchangeName, string queueName, int messageCount = 10)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  FANOUT EXCHANGE - Mesaj Tüketimi");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        try
        {
            var consumedCount = 0;

            // Fanout Exchange tanımlama
            await _channel.ExchangeDeclareAsync(
                exchangeName,
                ExchangeType.Fanout,
                true,
                false
            );

            // Queue tanımlama
            // Her consumer kendi queue'sunu oluşturur
            await _channel.QueueDeclareAsync(
                queueName,
                true, // durable
                false, // exclusive
                false
            );

            // Queue'yu Exchange'e bind et
            // Fanout'ta routing key kullanılmaz (boş string)
            // QueueBindAsync parametreleri:
            // 1. queue: Bind edilecek queue adı
            // 2. exchange: Bağlanılacak exchange adı
            // 3. routingKey: Fanout'ta önemsiz (boş string)
            // 4. arguments: Ek argümanlar (genelde null)
            await _channel.QueueBindAsync(
                queueName,
                exchangeName,
                string.Empty
            );

            Console.WriteLine("🎧 Fanout Exchange dinleniyor");
            Console.WriteLine($"   Exchange: {exchangeName}");
            Console.WriteLine($"   Queue: {queueName}");
            Console.WriteLine("   Type: Fanout (Broadcast)");
            Console.WriteLine($"   Hedef mesaj sayısı: {messageCount}\n");

            // QoS ayarı - Aynı anda kaç mesaj işlenecek
            await _channel.BasicQosAsync(0, 1, false);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"📥 Mesaj alındı: {message}");
                Console.WriteLine($"   Queue: {queueName}");
                Console.WriteLine($"   MessageId: {ea.BasicProperties.MessageId}");
                Console.WriteLine($"   Timestamp: {ea.BasicProperties.Timestamp}");
                Console.WriteLine($"   DeliveryTag: {ea.DeliveryTag}");
                Console.WriteLine($"   Exchange: {ea.Exchange}");
                Console.WriteLine("   📢 Broadcast mesajı alındı\n");

                // Manuel acknowledgment
                await _channel.BasicAckAsync(ea.DeliveryTag, false);

                consumedCount++;
                await Task.CompletedTask;
            };

            // Consumer başlat
            var consumerTag = await _channel.BasicConsumeAsync(
                queueName,
                false, // autoAck = false (manuel ack)
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
            Console.WriteLine("   📢 Fanout broadcast tamamlandı\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}\n");
            throw;
        }
    }
}