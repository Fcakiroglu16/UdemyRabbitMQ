using System.Text.Json;
using RabbitMQ.Client;
using PatternExample.API.Models;
using PatternExample.API.Services;

namespace PatternExample.API.Producer;

public class UserEventPublisher : BaseEventPublisher
{
    private const string ExchangeName = "user-events-exchange";

    public UserEventPublisher(
        RabbitMQConnectionService connectionService,
        ILogger<UserEventPublisher> logger)
        : base(connectionService, logger)
    {
    }

    public async Task PublishUserCreatedAsync(
        UserCreatedEvent userCreatedEvent,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = GenerateIdempotencyKey(EventType.UserCreatedEvent, userCreatedEvent.UserId);

        await PublishAsync(
            ExchangeName,
            userCreatedEvent,
            EventType.UserCreatedEvent,
            idempotencyKey,
            cancellationToken);
    }
}
