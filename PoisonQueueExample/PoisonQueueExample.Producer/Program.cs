using RabbitMQ.Client;
using System.Text;

const string MainExchange = "poison-main-exchange";
const string MainQueue = "poison-main-queue";
const string MainRoutingKey = "poison.route";

const string DeadLetterExchange = "poison-dlx";
const string DeadLetterQueue = "poison-dead-letter-queue";
const string DeadLetterRoutingKey = "poison.failed";

// Quorum queue delivery limit
const int DeliveryLimit = 3;

var factory = new ConnectionFactory
{
    HostName = "localhost"
};

await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await SetupTopologyAsync(channel);
Console.WriteLine("Poison Queue Producer haz?r. Mesaj girin, bo? geçerseniz ç?k?l?r.\n");

while (true)
{
    Console.Write("Mesaj: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        break;
    }

    var body = Encoding.UTF8.GetBytes(input.Trim());
    var props = new BasicProperties
    {
        Persistent = true,
        ContentType = "text/plain",
        MessageId = Guid.NewGuid().ToString(),
        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    };

    await channel.BasicPublishAsync(
        exchange: MainExchange,
        routingKey: MainRoutingKey,
        mandatory: false,
        basicProperties: props,
        body: body);

    Console.WriteLine($"Gönderildi -> MessageId: {props.MessageId}\n");
}

Console.WriteLine("Producer kapat?l?yor...");

static async Task SetupTopologyAsync(IChannel channel)
{
    // Dead-letter (poison) altyap?s?
    await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false);
    await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
    await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterRoutingKey);

    // Ana altyap? (quorum + delivery limit)
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
