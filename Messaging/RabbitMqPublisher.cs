using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace ms_games.Messaging;

public sealed class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable, IDisposable
{
    private readonly IConnection _connection;
    private readonly IConfiguration _configuration;
    private IChannel? _channel;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        _configuration = configuration;

        var factory = new ConnectionFactory
        {
            HostName = RequireConfiguration("RabbitMq:Host"),
            Port = int.Parse(_configuration["RabbitMq:Port"] ?? "5672"),
            UserName = RequireConfiguration("RabbitMq:Username"),
            Password = RequireConfiguration("RabbitMq:Password"),
            VirtualHost = RequireConfiguration("RabbitMq:VirtualHost")
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
    }

    private string RequireConfiguration(string key)
    {
        return _configuration[key]
            ?? throw new InvalidOperationException($"RabbitMQ configuration '{key}' is not configured");
    }

    public async Task PublishAsync<T>(string queueName, T message) where T : class
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            throw new InvalidOperationException("RabbitMQ queue name is not configured.");
        }

        await EnsureChannelAsync();

        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var exchangeName = _configuration["RabbitMq:ExchangeName"];
        if (!string.IsNullOrWhiteSpace(exchangeName))
        {
            await _channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null);

            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: queueName);
        }

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await _channel.BasicPublishAsync(
            exchange: exchangeName ?? string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    private async Task EnsureChannelAsync()
    {
        if (_channel is null || _channel.IsClosed)
        {
            _channel = await _connection.CreateChannelAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            if (_channel.IsOpen)
            {
                await _channel.CloseAsync();
            }

            await _channel.DisposeAsync();
        }

        if (_connection.IsOpen)
        {
            await _connection.CloseAsync();
        }

        await _connection.DisposeAsync();
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }
}
