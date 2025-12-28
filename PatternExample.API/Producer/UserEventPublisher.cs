using System.Text.Json;
using PatternExample.API.Data;
using PatternExample.API.Models;

namespace PatternExample.API.Producer;

public class UserEventPublisher
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<UserEventPublisher> _logger;

    public UserEventPublisher(
        AppDbContext dbContext,
        ILogger<UserEventPublisher> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task PublishUserCreatedAsync(
        UserCreatedEvent userCreatedEvent,
        CancellationToken cancellationToken = default)
    {
        var messageId = Guid.NewGuid().ToString();
        var idempotencyKey = Guid.NewGuid().ToString();

        var message = new { Event = userCreatedEvent };
        var payload = JsonSerializer.Serialize(message);

        var outboxMessage = new OutboxMessage
        {
            MessageId = messageId,
            IdempotencyKey = idempotencyKey,
            EventType = EventType.UserCreatedEvent.ToString(),
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            RetryCount = 0
        };

        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "UserCreatedEvent saved to outbox - OutboxMessageId: {OutboxMessageId}, MessageId: {MessageId}, IdempotencyKey: {IdempotencyKey}",
            outboxMessage.Id,
            messageId,
            idempotencyKey);
    }
}
