namespace ms_games.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string queueName, T message) where T : class;
}
