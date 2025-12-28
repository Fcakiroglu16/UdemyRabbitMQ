using RabbitMQ.Client;

namespace PatternExample.API.Services;

public class RabbitMQConnectionService : IAsyncDisposable
{
    private IConnection? _connection;
    private readonly ILogger<RabbitMQConnectionService> _logger;

    public RabbitMQConnectionService(ILogger<RabbitMQConnectionService> logger)
    {
        _logger = logger;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null && _connection.IsOpen)
        {
            return _connection;
        }

        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest"
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _logger.LogInformation("RabbitMQ bağlantısı kuruldu");

        return _connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null && _connection.IsOpen)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _logger.LogInformation("RabbitMQ bağlantısı kapatıldı");
        }
    }
}
