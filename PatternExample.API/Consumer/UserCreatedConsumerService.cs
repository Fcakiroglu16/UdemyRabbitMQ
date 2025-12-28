using Microsoft.EntityFrameworkCore;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace PatternExample.API.Consumer;

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

        IConnection connection = await _connectionService.GetConnectionAsync(stoppingToken);
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
            Guid messageId = Guid.Empty;
            var idempotencyKey = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(ea.BasicProperties.MessageId) ||
                    !Guid.TryParse(ea.BasicProperties.MessageId, out messageId))
                {
                    _logger.LogWarning("MessageId header eksik veya gecersiz");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                if (ea.BasicProperties.Headers is null ||
                    !ea.BasicProperties.Headers.TryGetValue("IdempotencyKey", out var idempotencyKeyObj))
                {
                    _logger.LogWarning("IdempotencyKey header eksik - MessageId: {MessageId}", messageId);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                idempotencyKey = idempotencyKeyObj is byte[] bytes
                    ? Encoding.UTF8.GetString(bytes)
                    : idempotencyKeyObj?.ToString() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(idempotencyKey))
                {
                    _logger.LogWarning("IdempotencyKey bos - MessageId: {MessageId}", messageId);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);
                MessageWrapper? message = JsonSerializer.Deserialize<MessageWrapper>(messageJson);

                if (message?.Event is null)
                {
                    _logger.LogWarning("Event deserialize edilemedi - MessageId: {MessageId}", messageId);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                var isProcessed = await StoreMessageInInboxAsync(
                    messageId,
                    idempotencyKey,
                    message.Event,
                    messageJson,
                    stoppingToken);

                if (isProcessed)
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    _logger.LogInformation(
                        "Mesaj inbox'a kaydedildi - MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
                        messageId,
                        idempotencyKey);
                }
                else
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                    _logger.LogWarning(
                        "Mesaj inbox'a kaydedilemedi, kuyruga geri gonderildi - MessageId: {MessageId}",
                        messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesaj islenirken hata olustu - MessageId: {MessageId}", messageId);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("UserCreatedConsumerService baslatildi - Queue: {QueueName}", QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task<bool> StoreMessageInInboxAsync(
        Guid messageId,
        string idempotencyKey,
        UserCreatedEvent userCreatedEvent,
        string payload,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        IdempotencyRecord? existingIdempotencyRecord = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingIdempotencyRecord is not null &&
            existingIdempotencyRecord.Status == IdempotencyStatus.Processed)
        {
            _logger.LogInformation(
                "Bu mesaj daha once islendi, atlanıyor - IdempotencyKey: {IdempotencyKey}",
                idempotencyKey);
            return true;
        }

        InboxMessage? existingInboxMessage = await dbContext.InboxMessages
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingInboxMessage is not null)
        {
            _logger.LogInformation(
                "Mesaj zaten inbox'ta mevcut - IdempotencyKey: {IdempotencyKey}, Status: {Status}",
                idempotencyKey,
                existingInboxMessage.Status);
            return true;
        }

        var isInMemory = dbContext.Database.IsInMemory();
        var transaction = isInMemory ? null : await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var idempotencyRecord = new IdempotencyRecord
            {
                IdempotencyKey = idempotencyKey,
                MessageId = messageId,
                EventType = EventType.UserCreatedEvent,
                CreatedAt = DateTime.UtcNow,
                Status = IdempotencyStatus.Processing
            };

            dbContext.IdempotencyRecords.Add(idempotencyRecord);

            var inboxMessage = new InboxMessage
            {
                MessageId = messageId,
                IdempotencyKey = idempotencyKey,
                EventType = EventType.UserCreatedEvent,
                Payload = payload,
                CreatedAt = DateTime.UtcNow,
                Status = InboxMessageStatus.Pending,
                RetryCount = 0
            };

            dbContext.InboxMessages.Add(inboxMessage);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Mesaj inbox ve idempotency tablolarına kaydedildi - MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
                messageId,
                idempotencyKey);

            return true;
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _logger.LogError(
                ex,
                "Mesaj inbox'a kaydedilirken hata - MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
                messageId,
                idempotencyKey);
            return false;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UserCreatedConsumerService durduruluyor...");

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    private record MessageWrapper(UserCreatedEvent Event);
}
