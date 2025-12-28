namespace PatternExample.API.Models;

public class IdempotencyRecord
{
    public int Id { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public Guid MessageId { get; set; }
    public EventType EventType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public IdempotencyStatus Status { get; set; }
}
