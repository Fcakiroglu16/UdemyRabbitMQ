using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using PatternExample.API.Models;
using PatternExample.API.Services;

namespace PatternExample.API.Producer;

public class UserEventPublisher
{
    private const string ExchangeName = "user-events-exchange";
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger<UserEventPublisher> _logger;

    public UserEventPublisher(
        RabbitMQConnectionService connectionService,
        ILogger<UserEventPublisher> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    public async Task PublishUserCreatedAsync(
        UserCreatedEvent userCreatedEvent,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionService.GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var messageId = Guid.NewGuid();
        var idempotencyKey = $"{EventType.UserCreatedEvent}-{userCreatedEvent.UserId}";

        var message = new { Event = userCreatedEvent };
        var messageBody = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(messageBody);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = messageId.ToString(),
            Headers = new Dictionary<string, object?>
            {
                { "IdempotencyKey", Encoding.UTF8.GetBytes(idempotencyKey) },
                { "EventType", Encoding.UTF8.GetBytes(EventType.UserCreatedEvent.ToString()) }
            }
        };

        await channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "UserCreatedEvent published - MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
            messageId,
            idempotencyKey);

        await channel.CloseAsync(cancellationToken);
        await channel.DisposeAsync();
    }
}
