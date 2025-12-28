using Microsoft.EntityFrameworkCore;
using PatternExample.API.Consumer;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Producer;
using PatternExample.API.Services;
using RabbitMQ.Client;
using Scalar.AspNetCore;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("DiscountDb"));

builder.Services.AddSingleton<RabbitMQConnectionService>();
builder.Services.AddScoped<UserEventPublisher>();
builder.Services.AddHostedService<UserCreatedConsumerService>();
builder.Services.AddHostedService<InboxMessageProcessorService>();
builder.Services.AddHostedService<OutboxMessagePublisherService>();

WebApplication app = builder.Build();

// RabbitMQ bağlantısını test et
RabbitMQConnectionService rabbitMQService = app.Services.GetRequiredService<RabbitMQConnectionService>();
ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("RabbitMQ bağlantısı test ediliyor...");
    IConnection connection = await rabbitMQService.GetConnectionAsync();

    if (connection.IsOpen)
    {
        logger.LogInformation("✅ RabbitMQ bağlantısı başarılı! Host: {HostName}, Port: {Port}",
            "localhost", 5672);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ RabbitMQ bağlantısı başarısız! Lütfen RabbitMQ servisinin çalıştığından emin olun.");
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


app.MapPost("/users", async (UserEventPublisher publisher, CancellationToken cancellationToken) =>
{
    var userId = Guid.NewGuid();
    var userCreatedEvent = new UserCreatedEvent(
        userId,
        $"user{userId:N}@example.com",
        "Test User");

    await publisher.PublishUserCreatedAsync(userCreatedEvent, cancellationToken);

    return Results.Ok(new
    {
        Message = "User created event published",
        UserId = userId,
        Event = userCreatedEvent
    });
})
.WithName("CreateUser");

app.MapGet("/discounts", async (AppDbContext dbContext) =>
{
    List<Discount> discounts = await dbContext.Discounts.ToListAsync();
    return Results.Ok(discounts);
})
.WithName("GetDiscounts");

app.MapGet("/idempotency-records", async (AppDbContext dbContext) =>
{
    List<IdempotencyRecord> records = await dbContext.IdempotencyRecords
        .OrderByDescending(i => i.CreatedAt)
        .ToListAsync();
    return Results.Ok(records);
})
.WithName("GetIdempotencyRecords");

app.MapGet("/outbox-messages", async (AppDbContext dbContext) =>
{
    List<OutboxMessage> messages = await dbContext.OutboxMessages
        .OrderByDescending(o => o.CreatedAt)
        .ToListAsync();
    return Results.Ok(messages);
})
.WithName("GetOutboxMessages");

app.MapGet("/inbox-messages", async (AppDbContext dbContext) =>
{
    List<InboxMessage> messages = await dbContext.InboxMessages
        .OrderByDescending(i => i.CreatedAt)
        .ToListAsync();
    return Results.Ok(messages);
})
.WithName("GetInboxMessages");

app.Run();

