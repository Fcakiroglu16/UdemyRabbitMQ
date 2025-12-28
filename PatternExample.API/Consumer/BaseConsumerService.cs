using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PatternExample.API.Services;

namespace PatternExample.API.Consumer;

public abstract class BaseConsumerService : BackgroundService
{
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger _logger;
    private IChannel? _channel;

    protected BaseConsumerService(
        RabbitMQConnectionService connectionService,
        ILogger logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    protected abstract string ExchangeName { get; }
    protected abstract string QueueName { get; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        var connection = await _connectionService.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: string.Empty,
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var (isValid, messageId, idempotencyKey) = ValidateMessageHeaders(ea.BasicProperties);

                if (!isValid)
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);

                var isProcessed = await ProcessMessageAsync(messageId, idempotencyKey, messageJson, stoppingToken);

                if (isProcessed)
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    _logger.LogInformation(
                        "Message {MessageId} with IdempotencyKey {IdempotencyKey} processed successfully",
                        messageId,
                        idempotencyKey);
                }
                else
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                    _logger.LogWarning("Message {MessageId} processing failed, requeued", messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("{ConsumerName} started listening on queue: {QueueName}",
            GetType().Name, QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    protected abstract Task<bool> ProcessMessageAsync(
        Guid messageId,
        string idempotencyKey,
        string messageJson,
        CancellationToken cancellationToken);

    private (bool IsValid, Guid MessageId, string IdempotencyKey) ValidateMessageHeaders(IReadOnlyBasicProperties properties)
    {
        if (string.IsNullOrWhiteSpace(properties.MessageId) ||
            !Guid.TryParse(properties.MessageId, out var messageId))
        {
            _logger.LogWarning("Failed to parse MessageId from message headers");
            return (false, Guid.Empty, string.Empty);
        }

        if (properties.Headers is null ||
            !properties.Headers.TryGetValue("IdempotencyKey", out var idempotencyKeyObj))
        {
            _logger.LogWarning("Message {MessageId} does not contain IdempotencyKey header", messageId);
            return (false, messageId, string.Empty);
        }

        var idempotencyKey = idempotencyKeyObj switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string str => str,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _logger.LogWarning("Message {MessageId} has empty IdempotencyKey", messageId);
            return (false, messageId, string.Empty);
        }

        return (true, messageId, idempotencyKey);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{ConsumerName} stopping", GetType().Name);

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
