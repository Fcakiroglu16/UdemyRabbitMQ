namespace PatternExample.API.Models;

public class OutboxMessage
{
    public int Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public bool IsProcessed { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}
