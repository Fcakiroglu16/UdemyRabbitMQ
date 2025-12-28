using Microsoft.EntityFrameworkCore;
using PatternExample.API.Data;
using PatternExample.API.Models;
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

app.Run();

