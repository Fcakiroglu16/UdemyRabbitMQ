using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

const string MainExchange = "poison-main-exchange";
const string MainQueue = "poison-main-queue";
const string MainRoutingKey = "poison.route";

const string DeadLetterExchange = "poison-dlx";
const string DeadLetterQueue = "poison-dead-letter-queue";
const string DeadLetterRoutingKey = "poison.failed";

const int DeliveryLimit = 3; // quorum delivery limit

var factory = new ConnectionFactory
{
    HostName = "localhost"
};

await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await SetupTopologyAsync(channel);
await channel.BasicQosAsync(0, 1, false);

Console.WriteLine("Poison Queue Consumer (quorum + delivery-limit). Broker 3. teslimde DLQ'ya yollar.\n");

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += async (_, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    var messageId = ea.BasicProperties.MessageId ?? Guid.NewGuid().ToString();

    Console.WriteLine($"Mesaj al?nd? | MessageId:{messageId}\n  ?çerik: {message}");

    // Hata simülasyonu: her zaman ba?ar?s?z.
    var processedSuccessfully = false;

    if (processedSuccessfully)
    {
        await channel.BasicAckAsync(ea.DeliveryTag, false);
        Console.WriteLine("Mesaj i?lendi ve ACK gönderildi.\n");
        return;
    }

    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
    Console.WriteLine($"??leme hatas? simüle edildi. requeue=true ile geri b?rak?ld?. Delivery-limit {DeliveryLimit} sonras?nda broker DLQ'ya ta??yacak.\n");
};

await channel.BasicConsumeAsync(
    queue: MainQueue,
    autoAck: false,
    consumer: consumer);

Console.WriteLine("Mesajlar bekleniyor... Ç?k?? için Ctrl+C\n");
await Task.Delay(Timeout.Infinite);

static async Task SetupTopologyAsync(IChannel channel)
{
    await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false);
    await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
    await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterRoutingKey);

    await channel.ExchangeDeclareAsync(MainExchange, ExchangeType.Direct, durable: true, autoDelete: false);
    var arguments = new Dictionary<string, object?>
    {
        { "x-queue-type", "quorum" },
        { "x-dead-letter-exchange", DeadLetterExchange },
        { "x-dead-letter-routing-key", DeadLetterRoutingKey },
        { "x-delivery-limit", DeliveryLimit }
    };
    await channel.QueueDeclareAsync(MainQueue, durable: true, exclusive: false, autoDelete: false, arguments: arguments);
    await channel.QueueBindAsync(MainQueue, MainExchange, MainRoutingKey);
}
