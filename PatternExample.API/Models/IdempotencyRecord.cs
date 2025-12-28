namespace PatternExample.API.Models;

public class IdempotencyRecord
{
    public int Id { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public Guid MessageId { get; set; }
    public string EventType { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string Status { get; set; } = default!;
}
