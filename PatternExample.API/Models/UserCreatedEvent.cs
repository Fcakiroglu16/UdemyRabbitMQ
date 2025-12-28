namespace PatternExample.API.Models;

public record UserCreatedEvent(Guid UserId, string Email, string FullName);
