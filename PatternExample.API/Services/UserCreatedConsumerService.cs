using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PatternExample.API.Data;
using PatternExample.API.Models;

namespace PatternExample.API.Services;

public class UserCreatedConsumerService : BackgroundService
{
    private const string ExchangeName = "user-events-exchange";
    private const string QueueName = "discount-queue";
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger<UserCreatedConsumerService> _logger;
    private IChannel? _channel;

    public UserCreatedConsumerService(
        IServiceProvider serviceProvider,
        RabbitMQConnectionService connectionService,
        ILogger<UserCreatedConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionService = connectionService;
        _logger = logger;
    }

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
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<MessageWrapper>(messageJson);

                if (message is null)
                {
                    _logger.LogWarning("Failed to deserialize message");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                if (string.IsNullOrWhiteSpace(message.IdempotencyKey))
                {
                    _logger.LogWarning("Message {MessageId} does not contain IdempotencyKey", message.MessageId);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                var isProcessed = await ProcessMessageWithIdempotencyAsync(
                    message.MessageId,
                    message.IdempotencyKey,
                    message.Event,
                    stoppingToken);

                if (isProcessed)
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    _logger.LogInformation(
                        "Message {MessageId} with IdempotencyKey {IdempotencyKey} processed successfully",
                        message.MessageId,
                        message.IdempotencyKey);
                }
                else
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                    _logger.LogWarning(
                        "Message {MessageId} processing failed, requeued",
                        message.MessageId);
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

        _logger.LogInformation("UserCreatedConsumerService started listening on queue: {QueueName}", QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task<bool> ProcessMessageWithIdempotencyAsync(
        Guid messageId,
        string idempotencyKey,
        UserCreatedEvent userCreatedEvent,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingRecord = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingRecord is not null)
        {
            if (existingRecord.Status == "Processed")
            {
                _logger.LogInformation(
                    "IdempotencyKey {IdempotencyKey} already processed at {ProcessedAt}, skipping",
                    idempotencyKey,
                    existingRecord.ProcessedAt);
                return true;
            }

            if (existingRecord.Status == "Processing")
            {
                _logger.LogWarning(
                    "IdempotencyKey {IdempotencyKey} is currently being processed, requeuing",
                    idempotencyKey);
                return false;
            }
        }

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (existingRecord is null)
            {
                var idempotencyRecord = new IdempotencyRecord
                {
                    IdempotencyKey = idempotencyKey,
                    MessageId = messageId,
                    EventType = "UserCreatedEvent",
                    CreatedAt = DateTime.UtcNow,
                    Status = "Processing"
                };

                dbContext.IdempotencyRecords.Add(idempotencyRecord);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                existingRecord.Status = "Processing";
                existingRecord.MessageId = messageId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var discount = new Discount
            {
                UserId = userCreatedEvent.UserId,
                DiscountPercentage = 10m,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Discounts.Add(discount);

            var processedMessage = new ProcessedMessage
            {
                MessageId = messageId,
                IdempotencyKey = idempotencyKey,
                ProcessedAt = DateTime.UtcNow
            };

            dbContext.ProcessedMessages.Add(processedMessage);

            var recordToUpdate = await dbContext.IdempotencyRecords
                .FirstAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

            recordToUpdate.Status = "Processed";
            recordToUpdate.ProcessedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Created 10% discount for UserId: {UserId} (Email: {Email}) with IdempotencyKey: {IdempotencyKey}",
                userCreatedEvent.UserId,
                userCreatedEvent.Email,
                idempotencyKey);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var failedRecord = await dbContext.IdempotencyRecords
                .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

            if (failedRecord is not null)
            {
                failedRecord.Status = "Failed";
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogError(
                ex,
                "Failed to create discount for UserId: {UserId} with IdempotencyKey: {IdempotencyKey}",
                userCreatedEvent.UserId,
                idempotencyKey);
            return false;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UserCreatedConsumerService stopping");
        
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    private record MessageWrapper(Guid MessageId, string IdempotencyKey, UserCreatedEvent Event);
}
