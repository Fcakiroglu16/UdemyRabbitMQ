using Microsoft.EntityFrameworkCore;
using PatternExample.API.Consumer;
using PatternExample.API.Data;
using PatternExample.API.Models;
using PatternExample.API.Producer;
using PatternExample.API.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("DiscountDb"));

builder.Services.AddSingleton<RabbitMQConnectionService>();
builder.Services.AddSingleton<UserEventPublisher>();
builder.Services.AddHostedService<UserCreatedConsumerService>();

WebApplication app = builder.Build();

// RabbitMQ ba?lant?s?n? test et
var rabbitMQService = app.Services.GetRequiredService<RabbitMQConnectionService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("RabbitMQ ba?lant?s? test ediliyor...");
    var connection = await rabbitMQService.GetConnectionAsync();
    
    if (connection.IsOpen)
    {
        logger.LogInformation("? RabbitMQ ba?lant?s? ba?ar?l?! Host: {HostName}, Port: {Port}", 
            "localhost", 5672);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "? RabbitMQ ba?lant?s? ba?ar?s?z! Lütfen RabbitMQ servisinin çal??t???ndan emin olun.");
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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
    var records = await dbContext.IdempotencyRecords
        .OrderByDescending(i => i.CreatedAt)
        .ToListAsync();
    return Results.Ok(records);
})
.WithName("GetIdempotencyRecords");

app.Run();

