namespace PatternExample.API.Models;

public class ProcessedMessage
{
    public int Id { get; set; }
    public Guid MessageId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public DateTime ProcessedAt { get; set; }
}
