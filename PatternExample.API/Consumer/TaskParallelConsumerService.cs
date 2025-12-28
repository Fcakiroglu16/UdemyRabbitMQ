using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Services;

namespace PatternExample.API.Consumer;

public class TaskParallelConsumerService : BackgroundService
{
    private const string ExchangeName = "user-events-exchange-parallel";
    private const string QueueName = "discount-queue-parallel";
    private const ushort PrefetchCount = 20;
    private const int MaxConcurrency = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger<TaskParallelConsumerService> _logger;
    private readonly ConcurrentQueue<BasicDeliverEventArgs> _messageQueue = new();
    private IChannel? _channel;

    public TaskParallelConsumerService(
        IServiceProvider serviceProvider,
        RabbitMQConnectionService connectionService,
        ILogger<TaskParallelConsumerService> logger)
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

        var processingTask = ProcessMessagesInParallelAsync(stoppingToken);

        _logger.LogInformation(
            "TaskParallelConsumerService ba?lat?ld? - Queue: {QueueName}, MaxDegreeOfParallelism: {MaxConcurrency}",
            QueueName,
            MaxConcurrency);

        await processingTask;
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
            _messageQueue.Enqueue(ea);
            await Task.CompletedTask;
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);
    }

    private async Task ProcessMessagesInParallelAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_messageQueue.IsEmpty)
            {
                await Task.Delay(100, stoppingToken);
                continue;
            }

            var batch = new List<BasicDeliverEventArgs>();
            while (batch.Count < MaxConcurrency && _messageQueue.TryDequeue(out var message))
            {
                batch.Add(message);
            }

            if (batch.Count == 0)
            {
                continue;
            }

            _logger.LogInformation(
                "Paralel i?leme ba?l?yor - Batch Size: {BatchSize}, Queue Size: {QueueSize}",
                batch.Count,
                _messageQueue.Count);

            await Parallel.ForEachAsync(
                batch,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxConcurrency,
                    CancellationToken = stoppingToken
                },
                async (ea, ct) => await HandleMessageAsync(ea, ct));
        }
    }

    private async ValueTask HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var messageId = Guid.Empty;
        var idempotencyKey = string.Empty;
        var threadId = Environment.CurrentManagedThreadId;

        try
        {
            _logger.LogInformation(
                "Mesaj i?leniyor (Parallel) - Thread: {ThreadId}, DeliveryTag: {DeliveryTag}",
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

            var isProcessed = await ProcessMessageAsync(messageId, idempotencyKey, userCreatedEvent, threadId, stoppingToken);

            if (isProcessed)
            {
                await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                _logger.LogInformation(
                    "Mesaj ba?ar?yla i?lendi (Parallel) - Thread: {ThreadId}, MessageId: {MessageId}",
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
            _logger.LogError(ex, "Mesaj i?lenirken hata (Parallel) - Thread: {ThreadId}, MessageId: {MessageId}", threadId, messageId);
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
        _logger.LogInformation("TaskParallelConsumerService durduruluyor...");

        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    private record MessageWrapper(UserCreatedEvent Event);
}
