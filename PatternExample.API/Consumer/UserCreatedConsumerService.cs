using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Services;

namespace PatternExample.API.Consumer;

public class UserCreatedConsumerService : BaseConsumerService
{
    private readonly IServiceProvider _serviceProvider;

    public UserCreatedConsumerService(
        IServiceProvider serviceProvider,
        RabbitMQConnectionService connectionService,
        ILogger<UserCreatedConsumerService> logger)
        : base(connectionService, logger)
    {
        _serviceProvider = serviceProvider;
    }

    protected override string ExchangeName => "user-events-exchange";
    protected override string QueueName => "discount-queue";

    protected override async Task<bool> ProcessMessageAsync(
        Guid messageId,
        string idempotencyKey,
        string messageJson,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<MessageWrapper>(messageJson);
        if (message?.Event is null)
        {
            return false;
        }

        return await ProcessMessageWithIdempotencyAsync(
            messageId,
            idempotencyKey,
            message.Event,
            cancellationToken);
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
                return true;
            }

            if (existingRecord.Status == IdempotencyStatus.Processing)
            {
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

            return false;
        }
    }

    private record MessageWrapper(UserCreatedEvent Event);
}
