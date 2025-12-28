using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Services;

namespace PatternExample.API.Consumer;

public class SemaphoreSlimConsumerService : BackgroundService
{
    private const string ExchangeName = "user-events-exchange-semaphore";
    private const string QueueName = "discount-queue-semaphore";
    private const ushort PrefetchCount = 20;
    private const int MaxConcurrency = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger<SemaphoreSlimConsumerService> _logger;
    private readonly SemaphoreSlim _semaphore = new(MaxConcurrency, MaxConcurrency);
    private IChannel? _channel;

    public SemaphoreSlimConsumerService(
        IServiceProvider serviceProvider,
        RabbitMQConnectionService connectionService,
        ILogger<SemaphoreSlimConsumerService> logger)
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
            "SemaphoreSlimConsumerService ba?lat?ld? - Queue: {QueueName}, MaxConcurrency: {MaxConcurrency}",
            QueueName,
            MaxConcurrency);

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
            await _semaphore.WaitAsync(stoppingToken);
            try
            {
                await HandleMessageAsync(ea, stoppingToken);
            }
            finally
            {
                _semaphore.Release();
            }
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
        var availableSlots = MaxConcurrency - _semaphore.CurrentCount;

        try
        {
            _logger.LogInformation(
                "Mesaj i?leniyor (SemaphoreSlim) - Thread: {ThreadId}, DeliveryTag: {DeliveryTag}, Active: {Active}/{Max}",
                threadId,
                ea.DeliveryTag,
                availableSlots,
                MaxConcurrency);

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

            var isProcessed = await ProcessMessageAsync(messageId, idempotencyKey, userCreatedEvent, threadId, stoppingToken);

            if (isProcessed)
            {
                await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                _logger.LogInformation(
                    "Mesaj ba?ar?yla i?lendi (SemaphoreSlim) - Thread: {ThreadId}, MessageId: {MessageId}",
                    threadId,
                    messageId);
            }
            else
            {
                await _channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mesaj i?lenirken hata (SemaphoreSlim) - Thread: {ThreadId}, MessageId: {MessageId}", threadId, messageId);
            await RejectMessageAsync(ea.DeliveryTag, requeue: true, stoppingToken);
        }
    }

    private bool TryExtractMessageId(BasicDeliverEventArgs ea, out Guid messageId)
    {
        messageId = Guid.Empty;
        return !string.IsNullOrWhiteSpace(ea.BasicProperties.MessageId) &&
               Guid.TryParse(ea.BasicProperties.MessageId, out messageId);
    }

    private bool TryExtractIdempotencyKey(BasicDeliverEventArgs ea, Guid messageId, out string idempotencyKey)
    {
        idempotencyKey = string.Empty;

        if (ea.BasicProperties.Headers is null ||
            !ea.BasicProperties.Headers.TryGetValue("IdempotencyKey", out var idempotencyKeyObj))
        {
            return false;
        }

        idempotencyKey = idempotencyKeyObj is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : idempotencyKeyObj?.ToString() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(idempotencyKey);
    }

    private UserCreatedEvent? DeserializeMessage(BasicDeliverEventArgs ea, Guid messageId)
    {
        try
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);
            var message = JsonSerializer.Deserialize<MessageWrapper>(messageJson);
            return message?.Event;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> ProcessMessageAsync(
        Guid messageId,
        string idempotencyKey,
        UserCreatedEvent userCreatedEvent,
        int threadId,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingRecord = await dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(i => i.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingRecord?.Status == IdempotencyStatus.Processed)
        {
            return true;
        }

        var discount = new Discount
        {
            UserId = userCreatedEvent.UserId,
            DiscountPercentage = 10m,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Discounts.Add(discount);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task RejectMessageAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken)
    {
        await _channel.BasicNackAsync(deliveryTag, false, requeue, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SemaphoreSlimConsumerService durduruluyor...");

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        _semaphore.Dispose();
        await base.StopAsync(cancellationToken);
    }

    private record MessageWrapper(UserCreatedEvent Event);
}
