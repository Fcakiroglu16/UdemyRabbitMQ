namespace PatternExample.API.Models;

public class InboxMessage
{
    public int Id { get; set; }
    public Guid MessageId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public EventType EventType { get; set; }
    public string Payload { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public InboxMessageStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}
