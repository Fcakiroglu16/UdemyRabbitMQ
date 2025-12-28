using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Services;

namespace PatternExample.API.Consumer;

public class ChannelBasedConsumerService : BackgroundService
{
    private const string ExchangeName = "user-events-exchange-channel";
    private const string QueueName = "discount-queue-channel";
    private const ushort PrefetchCount = 20;
    private const int MaxConcurrency = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger<ChannelBasedConsumerService> _logger;
    private readonly Channel<MessageContext> _messageChannel;
    private IChannel? _rabbitChannel;

    public ChannelBasedConsumerService(
        IServiceProvider serviceProvider,
        RabbitMQConnectionService connectionService,
        ILogger<ChannelBasedConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionService = connectionService;
        _logger = logger;
        _messageChannel = Channel.CreateBounded<MessageContext>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        await InitializeChannelAsync(stoppingToken);
        
        var processingTasks = Enumerable.Range(0, MaxConcurrency)
            .Select(i => ProcessMessagesFromChannelAsync(i, stoppingToken))
            .ToArray();

        await StartConsumingAsync(stoppingToken);

        _logger.LogInformation(
            "ChannelBasedConsumerService ba?lat?ld? - Queue: {QueueName}, Workers: {Workers}",
            QueueName,
            MaxConcurrency);

        await Task.WhenAll(processingTasks);
    }

    private async Task InitializeChannelAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionService.GetConnectionAsync(cancellationToken);
        _rabbitChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _rabbitChannel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _rabbitChannel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await _rabbitChannel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await _rabbitChannel.BasicQosAsync(0, PrefetchCount, false, cancellationToken);
    }

    private async Task StartConsumingAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_rabbitChannel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var messageContext = new MessageContext(ea, DateTime.UtcNow);
            await _messageChannel.Writer.WriteAsync(messageContext, stoppingToken);
        };

        await _rabbitChannel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);
    }

    private async Task ProcessMessagesFromChannelAsync(int workerId, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} ba?lat?ld?", workerId);

        await foreach (var messageContext in _messageChannel.Reader.ReadAllAsync(stoppingToken))
        {
            var threadId = Environment.CurrentManagedThreadId;
            var ea = messageContext.EventArgs;
            var messageId = Guid.Empty;
            var idempotencyKey = string.Empty;
            var waitTime = DateTime.UtcNow - messageContext.ReceivedAt;

            try
            {
                _logger.LogInformation(
                    "Mesaj i?leniyor (Channel) - Worker: {WorkerId}, Thread: {ThreadId}, WaitTime: {WaitMs}ms",
                    workerId,
                    threadId,
                    waitTime.TotalMilliseconds);

                if (!TryExtractMessageId(ea, out messageId))
                {
                    await RejectMessageAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                    continue;
                }

                if (!TryExtractIdempotencyKey(ea, messageId, out idempotencyKey))
                {
                    await RejectMessageAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                    continue;
                }

                var userCreatedEvent = DeserializeMessage(ea, messageId);
                if (userCreatedEvent is null)
                {
                    await RejectMessageAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                    continue;
                }

                var isProcessed = await ProcessMessageAsync(messageId, idempotencyKey, userCreatedEvent, workerId, threadId, stoppingToken);

                if (isProcessed)
                {
                    await _rabbitChannel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    _logger.LogInformation(
                        "Mesaj ba?ar?yla i?lendi (Channel) - Worker: {WorkerId}, MessageId: {MessageId}",
                        workerId,
                        messageId);
                }
                else
                {
                    await _rabbitChannel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Mesaj i?lenirken hata (Channel) - Worker: {WorkerId}, MessageId: {MessageId}", workerId, messageId);
                await RejectMessageAsync(ea.DeliveryTag, requeue: true, stoppingToken);
            }
        }

        _logger.LogInformation("Worker {WorkerId} durduruluyor", workerId);
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
        int workerId,
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
        await _rabbitChannel.BasicNackAsync(deliveryTag, false, requeue, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ChannelBasedConsumerService durduruluyor...");

        _messageChannel.Writer.Complete();

        if (_rabbitChannel is not null)
        {
            await _rabbitChannel.CloseAsync(cancellationToken);
            _rabbitChannel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    private record MessageWrapper(UserCreatedEvent Event);
    private record MessageContext(BasicDeliverEventArgs EventArgs, DateTime ReceivedAt);
}
