#region

using RabbitMQ.Client;
using System.Text;

#endregion

namespace Producer;

/// <summary>
///     RabbitMQ'ya basit mesaj gönderme işlemleri.
///     Publisher Confirms (Acknowledgment) açık ve kapalı senaryoları.
/// </summary>
public class RabbitMQAckSimplePublisher
{
    private readonly IChannel _channel;

    public RabbitMQAckSimplePublisher(IConnection connection)
    {
        var channelOpts = new CreateChannelOptions(
            true,
            true,
            new ThrottlingRateLimiter(100)
        );
        _channel = connection.CreateChannelAsync(channelOpts).Result;
    }

    /// <summary>
    ///     Metod 1: Publisher Confirms KAPALI - Fire and Forget
    ///     - Mesaj gönderilir, onay beklenmez
    ///     - En hızlı yöntem
    ///     - Mesajın RabbitMQ'ya ulaşıp ulaşmadığı garanti edilmez
    ///     - Kullanım: Kritik olmayan, kaybolması kabul edilebilir mesajlar (log, metric)
    /// </summary>
    public async Task SendMessageWithoutAcknowledgment(string queueName, string message)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  ACK KAPALI - Fire and Forget Modu");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");

        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = Guid.NewGuid().ToString()
        };

        // Mesajı gönder - onay bekleme
        await _channel.BasicPublishAsync(
            string.Empty,
            queueName,
            false,
            properties,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi: {message}");
        Console.WriteLine($"   Kuyruk: {queueName}");
        Console.WriteLine($"   MessageId: {properties.MessageId}");
        Console.WriteLine("   ⚠️  Onay beklenmedi (Fire and Forget)");
        Console.WriteLine("   ⚡ Çok hızlı gönderim");
        Console.WriteLine("   ⚠️  Mesajın ulaştığı garanti edilmez\n");
    }

    /// <summary>
    ///     Metod 2: Publisher Confirms AÇIK - Güvenli Gönderim
    ///     - Mesaj gönderilir ve RabbitMQ'dan onay beklenir
    ///     - Mesajın RabbitMQ'ya ulaştığı garanti edilir
    ///     - Biraz daha yavaş ama güvenilir
    ///     - Kullanım: Kritik mesajlar (order, payment, user data)
    /// </summary>
    public async Task SendMessageWithAcknowledgment(string queueName, string message)
    {
        Console.WriteLine("\n═══════════════════════════════════════════════════════════");
        Console.WriteLine("  ACK AÇIK - Publisher Confirms Modu");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");


        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = Guid.NewGuid().ToString()
        };
        var queueDeclareResult = await _channel.QueueDeclareAsync(queueName, true, false, false);

        // Mesajı gönder
        await _channel.BasicPublishAsync(
            string.Empty,
            queueName,
            true,
            properties,
            body
        );

        Console.WriteLine($"📤 Mesaj gönderildi: {message}");
        Console.WriteLine($"   Kuyruk: {queueName}");
        Console.WriteLine($"   MessageId: {properties.MessageId}");
        Console.WriteLine("   ⏳ RabbitMQ'dan onay bekleniyor...");


        Console.WriteLine("   ✅ RabbitMQ onayı alındı!");
        Console.WriteLine("   🔒 Mesaj başarıyla RabbitMQ'ya ulaştı");
        Console.WriteLine("   ✓ Güvenli gönderim\n");
    }
}