#region

using Consumer.Stream;
using RabbitMQ.Client;

#endregion

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║      RabbitMQ Stream Consumer - ACK ve Offset Senaryoları   ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

// RabbitMQ bağlantı ayarları (Producer ile aynı broker)
var factory = new ConnectionFactory
{
    Uri = new Uri("amqps://ffqycmjd:cYOzydQzAx6gSvtbPdF4zU5T-2r4c-UR@gorilla.lmq.cloudamqp.com/ffqycmjd")
};

await using var connection = await factory.CreateConnectionAsync();

// Producer tarafındaki StreamAckPublisher ile aynı stream adı kullanılır.
const string streamName = "stream-ack-demo";

// ═══════════════════════════════════════════════════════════════════════
// 1) ACK KAPALI - Otomatik onay (autoAck = true)
// ═══════════════════════════════════════════════════════════════════════
var ackConsumer = new StreamAckConsumer(connection);

await ackConsumer.ConsumeAsync(streamName, manualAck: false, 5, "first");

// ═══════════════════════════════════════════════════════════════════════
// 2) ACK AÇIK - Manuel onay (autoAck = false, QoS/prefetch zorunlu)
// ═══════════════════════════════════════════════════════════════════════
await ackConsumer.ConsumeAsync(streamName, manualAck: true, 5, "first");

// ═══════════════════════════════════════════════════════════════════════
// 3) OFFSET ARALIĞI - [2, 4] offsetleri arasındaki mesajları oku
// ═══════════════════════════════════════════════════════════════════════
var offsetConsumer = new StreamOffsetConsumer(connection);

await offsetConsumer.ConsumeByOffsetRangeAsync(streamName, 2, 4);

// ═══════════════════════════════════════════════════════════════════════
// 4) OFFSET'TEN İTİBAREN - 1. offsetten başlayarak 3 mesaj oku
// ═══════════════════════════════════════════════════════════════════════
await offsetConsumer.ConsumeFromOffsetAsync(streamName, 1, 3);

Console.WriteLine("\n─────────────────────────────────────────────────────────────");
Console.WriteLine("Çıkmak için bir tuşa basın...");
Console.ReadKey();
