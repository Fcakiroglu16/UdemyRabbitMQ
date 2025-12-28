using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using PatternExample.API.Models;
using PatternExample.API.Services;

namespace PatternExample.API.Producer;

public abstract class BaseEventPublisher
{
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger _logger;

    protected BaseEventPublisher(
        RabbitMQConnectionService connectionService,
        ILogger logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    protected async Task PublishAsync<TEvent>(
        string exchangeName,
        TEvent @event,
        EventType eventType,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionService.GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var messageId = Guid.NewGuid();

        var message = new
        {
            Event = @event
        };

        var messageBody = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(messageBody);

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = messageId.ToString(),
            Headers = new Dictionary<string, object?>
            {
                { "IdempotencyKey", Encoding.UTF8.GetBytes(idempotencyKey) },
                { "EventType", Encoding.UTF8.GetBytes(eventType.ToString()) }
            }
        };

        await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "{EventType} published with MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
            eventType,
            messageId,
            idempotencyKey);

        await channel.CloseAsync(cancellationToken);
        await channel.DisposeAsync();
    }

    protected static string GenerateIdempotencyKey(EventType eventType, Guid entityId)
    {
        return $"{eventType}-{entityId}";
    }
}
