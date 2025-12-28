using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatternExample.API.Data;
using PatternExample.API.Models;

namespace PatternExample.API.Consumer;

public class InboxMessageProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InboxMessageProcessorService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(5);

    public InboxMessageProcessorService(
        IServiceProvider serviceProvider,
        ILogger<InboxMessageProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InboxMessageProcessorService baslatildi");

        await Task.Delay(3000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inbox mesajlari islenirken hata olustu");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingMessages = await dbContext.InboxMessages
            .Where(m => m.Status == InboxMessageStatus.Pending || 
                       (m.Status == InboxMessageStatus.Failed && m.RetryCount < 3))
            .OrderBy(m => m.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Islenmek uzere {Count} mesaj bulundu", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            await ProcessMessageAsync(message, dbContext, cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(
        InboxMessage inboxMessage,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var isInMemory = dbContext.Database.IsInMemory();
        var transaction = isInMemory ? null : await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            inboxMessage.Status = InboxMessageStatus.Processing;
            await dbContext.SaveChangesAsync(cancellationToken);

            var existingIdempotencyRecord = await dbContext.IdempotencyRecords
                .FirstOrDefaultAsync(i => i.IdempotencyKey == inboxMessage.IdempotencyKey, cancellationToken);

            if (existingIdempotencyRecord is not null && 
                existingIdempotencyRecord.Status == IdempotencyStatus.Processed)
            {
                _logger.LogInformation(
                    "Mesaj zaten islenmis, inbox kayd? guncelleniyor - IdempotencyKey: {IdempotencyKey}",
                    inboxMessage.IdempotencyKey);

                inboxMessage.Status = InboxMessageStatus.Processed;
                inboxMessage.ProcessedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return;
            }

            var messageWrapper = JsonSerializer.Deserialize<MessageWrapper>(inboxMessage.Payload);
            if (messageWrapper?.Event is null)
            {
                throw new InvalidOperationException("Event deserialize edilemedi");
            }

            var idempotencyRecord = new IdempotencyRecord
            {
                IdempotencyKey = inboxMessage.IdempotencyKey,
                MessageId = inboxMessage.MessageId,
                EventType = inboxMessage.EventType,
                CreatedAt = DateTime.UtcNow,
                Status = IdempotencyStatus.Processing
            };

            dbContext.IdempotencyRecords.Add(idempotencyRecord);
            await dbContext.SaveChangesAsync(cancellationToken);

            var discount = new Discount
            {
                UserId = messageWrapper.Event.UserId,
                DiscountPercentage = 10m,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Discounts.Add(discount);

            var recordToUpdate = await dbContext.IdempotencyRecords
                .FirstAsync(i => i.IdempotencyKey == inboxMessage.IdempotencyKey, cancellationToken);

            recordToUpdate.Status = IdempotencyStatus.Processed;
            recordToUpdate.ProcessedAt = DateTime.UtcNow;

            inboxMessage.Status = InboxMessageStatus.Processed;
            inboxMessage.ProcessedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Inbox mesaji basariyla islendi - MessageId: {MessageId}, UserId: {UserId}",
                inboxMessage.MessageId,
                messageWrapper.Event.UserId);
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            inboxMessage.Status = InboxMessageStatus.Failed;
            inboxMessage.RetryCount++;
            inboxMessage.ErrorMessage = ex.Message;

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Inbox mesaji islenirken hata - MessageId: {MessageId}, RetryCount: {RetryCount}",
                inboxMessage.MessageId,
                inboxMessage.RetryCount);
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private record MessageWrapper(UserCreatedEvent Event);
}
