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
    private const ushort PrefetchCount = 20;
    private const int ConsumerCount = 20;

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

        await InitializeChannelAsync(stoppingToken);
        await StartConsumingAsync(stoppingToken);

        _logger.LogInformation(
            "UserCreatedConsumerService ba?lat?ld? - Queue: {QueueName}, PrefetchCount: {PrefetchCount}, ConsumerCount: {ConsumerCount}",
            QueueName,
            PrefetchCount,
            ConsumerCount);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task InitializeChannelAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionService.GetConnectionAsync(cancellationToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await _channel.BasicQosAsync(0, PrefetchCount, false, cancellationToken);
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            await Task.Run(async () => await HandleMessageAsync(ea, stoppingToken), stoppingToken);
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var messageId = Guid.Empty;
        var idempotencyKey = string.Empty;
        var threadId = Environment.CurrentManagedThreadId;

        try
        {
            _logger.LogInformation(
                "Mesaj i?leniyor - Thread: {ThreadId}, DeliveryTag: {DeliveryTag}",
                threadId,
                ea.DeliveryTag);

            if (!TryExtractMessageId(ea, out messageId))
            {
                await RejectMessageAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                return;
            }

            if (!TryExtractIdempotencyKey(ea, messageId, out idempotencyKey))
            {
                await RejectMessageAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                return;
            }

            var userCreatedEvent = DeserializeMessage(ea, messageId);
            if (userCreatedEvent is null)
            {
                await RejectMessageAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                return;
            }

            var isProcessed = await ProcessMessageWithIdempotencyAsync(
                messageId,
                idempotencyKey,
                userCreatedEvent,
                threadId,
                stoppingToken);

            if (isProcessed)
            {
                await AcknowledgeMessageAsync(ea.DeliveryTag, messageId, idempotencyKey, threadId, stoppingToken);
            }
            else
            {
                await RequeueMessageAsync(ea.DeliveryTag, messageId, threadId, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Mesaj i?lenirken hata olu?tu - Thread: {ThreadId}, MessageId: {MessageId}",
                threadId,
                messageId);
            await RejectMessageAsync(ea.DeliveryTag, requeue: true, stoppingToken);
        }
    }

    private bool TryExtractMessageId(BasicDeliverEventArgs ea, out Guid messageId)
    {
        messageId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(ea.BasicProperties.MessageId) ||
            !Guid.TryParse(ea.BasicProperties.MessageId, out messageId))
        {
            _logger.LogWarning("MessageId header eksik veya geçersiz");
            return false;
        }

        return true;
    }

    private bool TryExtractIdempotencyKey(BasicDeliverEventArgs ea, Guid messageId, out string idempotencyKey)
    {
        idempotencyKey = string.Empty;

        if (ea.BasicProperties.Headers is null ||
            !ea.BasicProperties.Headers.TryGetValue("IdempotencyKey", out var idempotencyKeyObj))
        {
            _logger.LogWarning("IdempotencyKey header eksik - MessageId: {MessageId}", messageId);
            return false;
        }

        idempotencyKey = idempotencyKeyObj is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : idempotencyKeyObj?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _logger.LogWarning("IdempotencyKey bo? - MessageId: {MessageId}", messageId);
            return false;
        }

        return true;
    }

    private UserCreatedEvent? DeserializeMessage(BasicDeliverEventArgs ea, Guid messageId)
    {
        try
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);
            var message = JsonSerializer.Deserialize<MessageWrapper>(messageJson);

            if (message?.Event is null)
            {
                _logger.LogWarning("Event deserialize edilemedi - MessageId: {MessageId}", messageId);
                return null;
            }

            return message.Event;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj deserialize edilirken hata - MessageId: {MessageId}", messageId);
            return null;
        }
    }

    private async Task<bool> ProcessMessageWithIdempotencyAsync(
        Guid messageId,
        string idempotencyKey,
        UserCreatedEvent userCreatedEvent,
        int threadId,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await IsAlreadyProcessedAsync(dbContext, idempotencyKey, threadId, cancellationToken))
        {
            return true;
        }

        if (await IsCurrentlyProcessingAsync(dbContext, idempotencyKey, threadId, cancellationToken))
        {
            return false;
        }

        return await ProcessMessageInTransactionAsync(
            dbContext,
            messageId,
            idempotencyKey,
            userCreatedEvent,
            threadId,
            cancellationToken);
    }

    private async Task<bool> IsAlreadyProcessedAsync(
        AppDbContext dbContext,
        string idempotencyKey,
        int threadId,
        CancellationToken cancellationToken)
    {
        var existingRecord = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingRecord?.Status == IdempotencyStatus.Processed)
        {
            _logger.LogInformation(
                "Bu mesaj daha önce i?lendi, atlan?yor - Thread: {ThreadId}, IdempotencyKey: {IdempotencyKey}",
                threadId,
                idempotencyKey);
            return true;
        }

        return false;
    }

    private async Task<bool> IsCurrentlyProcessingAsync(
        AppDbContext dbContext,
        string idempotencyKey,
        int threadId,
        CancellationToken cancellationToken)
    {
        var existingRecord = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingRecord?.Status == IdempotencyStatus.Processing)
        {
            _logger.LogWarning(
                "Bu mesaj ?u anda i?leniyor, kuyru?a geri gönderiliyor - Thread: {ThreadId}, IdempotencyKey: {IdempotencyKey}",
                threadId,
                idempotencyKey);
            return true;
        }

        return false;
    }

    private async Task<bool> ProcessMessageInTransactionAsync(
        AppDbContext dbContext,
        Guid messageId,
        string idempotencyKey,
        UserCreatedEvent userCreatedEvent,
        int threadId,
        CancellationToken cancellationToken)
    {
        var isInMemory = dbContext.Database.IsInMemory();
        var transaction = isInMemory ? null : await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await CreateOrUpdateIdempotencyRecordAsync(
                dbContext,
                messageId,
                idempotencyKey,
                cancellationToken);

            await CreateDiscountAsync(dbContext, userCreatedEvent, cancellationToken);

            await MarkAsProcessedAsync(dbContext, idempotencyKey, cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            LogSuccessfulProcessing(userCreatedEvent, threadId);
            return true;
        }
        catch (Exception ex)
        {
            await HandleProcessingErrorAsync(
                transaction,
                dbContext,
                idempotencyKey,
                userCreatedEvent,
                threadId,
                ex,
                cancellationToken);
            return false;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private async Task CreateOrUpdateIdempotencyRecordAsync(
        AppDbContext dbContext,
        Guid messageId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existingRecord = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

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
        }
        else
        {
            existingRecord.Status = IdempotencyStatus.Processing;
            existingRecord.MessageId = messageId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateDiscountAsync(
        AppDbContext dbContext,
        UserCreatedEvent userCreatedEvent,
        CancellationToken cancellationToken)
    {
        var discount = new Discount
        {
            UserId = userCreatedEvent.UserId,
            DiscountPercentage = 10m,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Discounts.Add(discount);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkAsProcessedAsync(
        AppDbContext dbContext,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var recordToUpdate = await dbContext.IdempotencyRecords
            .FirstAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

        recordToUpdate.Status = IdempotencyStatus.Processed;
        recordToUpdate.ProcessedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void LogSuccessfulProcessing(UserCreatedEvent userCreatedEvent, int threadId)
    {
        _logger.LogInformation(
            "Kullan?c? için %10 indirim olu?turuldu - Thread: {ThreadId}, UserId: {UserId}, Email: {Email}",
            threadId,
            userCreatedEvent.UserId,
            userCreatedEvent.Email);
    }

    private async Task HandleProcessingErrorAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction,
        AppDbContext dbContext,
        string idempotencyKey,
        UserCreatedEvent userCreatedEvent,
        int threadId,
        Exception ex,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        var failedRecord = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

        if (failedRecord is not null)
        {
            failedRecord.Status = IdempotencyStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogError(
            ex,
            "?ndirim olu?turulurken hata - Thread: {ThreadId}, UserId: {UserId}, IdempotencyKey: {IdempotencyKey}",
            threadId,
            userCreatedEvent.UserId,
            idempotencyKey);
    }

    private async Task AcknowledgeMessageAsync(
        ulong deliveryTag,
        Guid messageId,
        string idempotencyKey,
        int threadId,
        CancellationToken cancellationToken)
    {
        await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
        _logger.LogInformation(
            "Mesaj ba?ar?yla i?lendi - Thread: {ThreadId}, MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
            threadId,
            messageId,
            idempotencyKey);
    }

    private async Task RequeueMessageAsync(
        ulong deliveryTag,
        Guid messageId,
        int threadId,
        CancellationToken cancellationToken)
    {
        await _channel.BasicNackAsync(deliveryTag, false, true, cancellationToken);
        _logger.LogWarning(
            "Mesaj i?lenemedi, kuyru?a geri gönderildi - Thread: {ThreadId}, MessageId: {MessageId}",
            threadId,
            messageId);
    }

    private async Task RejectMessageAsync(
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        await _channel.BasicNackAsync(deliveryTag, false, requeue, cancellationToken);
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
