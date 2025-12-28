using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatternExample.API.Data;
using PatternExample.API.Models;
using RabbitMQ.Client;

namespace PatternExample.API.Services;

public class OutboxMessagePublisherService : BackgroundService
{
    private const string ExchangeName = "user-events-exchange";
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQConnectionService _connectionService;
    private readonly ILogger<OutboxMessagePublisherService> _logger;
    private readonly TimeSpan _publishInterval = TimeSpan.FromSeconds(5);

    public OutboxMessagePublisherService(
        IServiceProvider serviceProvider,
        RabbitMQConnectionService connectionService,
        ILogger<OutboxMessagePublisherService> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionService = connectionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxMessagePublisherService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
                await Task.Delay(_publishInterval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing outbox messages");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("OutboxMessagePublisherService stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(m => !m.IsProcessed)
            .OrderBy(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox messages", messages.Count);

        var connection = await _connectionService.GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message.Payload);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = message.MessageId,
                    Headers = new Dictionary<string, object?>
                    {
                        { "IdempotencyKey", Encoding.UTF8.GetBytes(message.IdempotencyKey) },
                        { "EventType", Encoding.UTF8.GetBytes(message.EventType) }
                    }
                };

                await channel.BasicPublishAsync(
                    exchange: ExchangeName,
                    routingKey: string.Empty,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken);

                message.IsProcessed = true;
                message.ProcessedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Outbox message published - Id: {Id}, MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
                    message.Id,
                    message.MessageId,
                    message.IdempotencyKey);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.ErrorMessage = ex.Message;

                _logger.LogError(ex,
                    "Failed to publish outbox message - Id: {Id}, RetryCount: {RetryCount}",
                    message.Id,
                    message.RetryCount);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await channel.CloseAsync(cancellationToken);
        await channel.DisposeAsync();
    }
}
