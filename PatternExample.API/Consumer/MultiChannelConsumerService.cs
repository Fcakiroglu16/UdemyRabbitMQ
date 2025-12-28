using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Services;

namespace PatternExample.API.Consumer;

public class MultiChannelConsumerService : BackgroundService
{
    private const string ExchangeName = "user-events-exchange-multichannel";
    private const string QueueName = "discount-queue-multichannel";
    private const ushort PrefetchCount = 1;
    private const int ChannelCount = 20;

    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger<MultiChannelConsumerService> _logger;
    private readonly List<IChannel> _channels = new();

    public MultiChannelConsumerService(
        IServiceProvider serviceProvider,
        RabbitMQConnectionService connectionService,
        ILogger<MultiChannelConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionService = connectionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        var connection = await _connectionService.GetConnectionAsync(stoppingToken);

        await InitializeQueueAsync(connection, stoppingToken);

        var consumerTasks = new List<Task>();
        for (int i = 0; i < ChannelCount; i++)
        {
            var channelId = i;
            consumerTasks.Add(StartChannelConsumerAsync(connection, channelId, stoppingToken));
        }

        _logger.LogInformation(
            "MultiChannelConsumerService ba?lat?ld? - Queue: {QueueName}, Channels: {ChannelCount}",
            QueueName,
            ChannelCount);

        await Task.WhenAll(consumerTasks);
    }

    private async Task InitializeQueueAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var setupChannel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await setupChannel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await setupChannel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await setupChannel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);

        await setupChannel.CloseAsync(cancellationToken);
        setupChannel.Dispose();
    }

    private async Task StartChannelConsumerAsync(IConnection connection, int channelId, CancellationToken stoppingToken)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _channels.Add(channel);

        await channel.BasicQosAsync(0, PrefetchCount, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            await HandleMessageAsync(channel, channelId, ea, stoppingToken);
        };

        await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Channel {ChannelId} consumer ba?lat?ld?", channelId);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        int channelId,
        BasicDeliverEventArgs ea,
        CancellationToken stoppingToken)
    {
        var messageId = Guid.Empty;
        var idempotencyKey = string.Empty;
        var threadId = Environment.CurrentManagedThreadId;

        try
        {
            _logger.LogInformation(
                "Mesaj i?leniyor (MultiChannel) - Channel: {ChannelId}, Thread: {ThreadId}, DeliveryTag: {DeliveryTag}",
                channelId,
                threadId,
                ea.DeliveryTag);

            if (!TryExtractMessageId(ea, out messageId))
            {
                await RejectMessageAsync(channel, ea.DeliveryTag, requeue: false, stoppingToken);
                return;
            }

            if (!TryExtractIdempotencyKey(ea, messageId, out idempotencyKey))
            {
                await RejectMessageAsync(channel, ea.DeliveryTag, requeue: false, stoppingToken);
                return;
            }

            var userCreatedEvent = DeserializeMessage(ea, messageId);
            if (userCreatedEvent is null)
            {
                await RejectMessageAsync(channel, ea.DeliveryTag, requeue: false, stoppingToken);
                return;
            }

            var isProcessed = await ProcessMessageAsync(messageId, idempotencyKey, userCreatedEvent, channelId, threadId, stoppingToken);

            if (isProcessed)
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                _logger.LogInformation(
                    "Mesaj ba?ar?yla i?lendi (MultiChannel) - Channel: {ChannelId}, MessageId: {MessageId}",
                    channelId,
                    messageId);
            }
            else
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Mesaj i?lenirken hata (MultiChannel) - Channel: {ChannelId}, MessageId: {MessageId}",
                channelId,
                messageId);
            await RejectMessageAsync(channel, ea.DeliveryTag, requeue: true, stoppingToken);
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
        int channelId,
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

    private async Task RejectMessageAsync(IChannel channel, ulong deliveryTag, bool requeue, CancellationToken cancellationToken)
    {
        await channel.BasicNackAsync(deliveryTag, false, requeue, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MultiChannelConsumerService durduruluyor...");

        foreach (var channel in _channels)
        {
            await channel.CloseAsync(cancellationToken);
            channel.Dispose();
        }

        _channels.Clear();

        await base.StopAsync(cancellationToken);
    }

    private record MessageWrapper(UserCreatedEvent Event);
}
