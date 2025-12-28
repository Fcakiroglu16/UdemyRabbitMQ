using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Services;

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
            var messageId = Guid.Empty;
            var idempotencyKey = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(ea.BasicProperties.MessageId) ||
                    !Guid.TryParse(ea.BasicProperties.MessageId, out messageId))
                {
                    _logger.LogWarning("MessageId header eksik veya geçersiz");
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
                    _logger.LogWarning("IdempotencyKey bo? - MessageId: {MessageId}", messageId);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<MessageWrapper>(messageJson);

                if (message?.Event is null)
                {
                    _logger.LogWarning("Event deserialize edilemedi - MessageId: {MessageId}", messageId);
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                    return;
                }

                var isProcessed = await ProcessMessageWithIdempotencyAsync(
                    messageId,
                    idempotencyKey,
                    message.Event,
                    stoppingToken);

                if (isProcessed)
                {
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    _logger.LogInformation(
                        "Mesaj ba?ar?yla i?lendi - MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
                        messageId,
                        idempotencyKey);
                }
                else
                {
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                    _logger.LogWarning(
                        "Mesaj i?lenemedi, kuyru?a geri gönderildi - MessageId: {MessageId}",
                        messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesaj i?lenirken hata olu?tu - MessageId: {MessageId}", messageId);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("UserCreatedConsumerService ba?lat?ld? - Queue: {QueueName}", QueueName);

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
            if (existingRecord.Status == IdempotencyStatus.Processed)
            {
                _logger.LogInformation(
                    "Bu mesaj daha önce i?lendi, atlan?yor - IdempotencyKey: {IdempotencyKey}",
                    idempotencyKey);
                return true;
            }

            if (existingRecord.Status == IdempotencyStatus.Processing)
            {
                _logger.LogWarning(
                    "Bu mesaj ?u anda i?leniyor, kuyru?a geri gönderiliyor - IdempotencyKey: {IdempotencyKey}",
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
                    EventType = EventType.UserCreatedEvent,
                    CreatedAt = DateTime.UtcNow,
                    Status = IdempotencyStatus.Processing
                };

                dbContext.IdempotencyRecords.Add(idempotencyRecord);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                existingRecord.Status = IdempotencyStatus.Processing;
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

            var recordToUpdate = await dbContext.IdempotencyRecords
                .FirstAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

            recordToUpdate.Status = IdempotencyStatus.Processed;
            recordToUpdate.ProcessedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Kullan?c? için %10 indirim olu?turuldu - UserId: {UserId}, Email: {Email}",
                userCreatedEvent.UserId,
                userCreatedEvent.Email);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            var failedRecord = await dbContext.IdempotencyRecords
                .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

            if (failedRecord is not null)
            {
                failedRecord.Status = IdempotencyStatus.Failed;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogError(
                ex,
                "?ndirim olu?turulurken hata - UserId: {UserId}, IdempotencyKey: {IdempotencyKey}",
                userCreatedEvent.UserId,
                idempotencyKey);
            return false;
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
