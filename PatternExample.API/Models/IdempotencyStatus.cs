namespace PatternExample.API.Models;

public enum IdempotencyStatus
{
    Processing,
    Processed,
    Failed
}
