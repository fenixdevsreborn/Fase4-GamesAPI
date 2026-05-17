using Amazon.DynamoDBv2;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using ms_games.Events;
using ms_games.Messaging;
using ms_games.Models;
using ms_games.Repositories;
using ms_games.Services;
using System.Text.Json;

namespace ms_games.Tests;

public class GameServiceTests
{
    [Fact]
    public async Task RequestPurchase_PublishesPurchaseRequestedEventToConfiguredQueue()
    {
        var publisher = new Mock<IMessagePublisher>();
        var service = CreateService(publisher: publisher);

        await service.RequestPurchase(
            userId: "user-123",
            email: "player@test.com",
            gameId: "game-123",
            gameName: "Halo",
            gameValue: 99.90m,
            amount: 1);

        publisher.Verify(
            p => p.PublishAsync(
                "custom-payment-queue",
                It.Is<PurchaseRequestedEvent>(e =>
                    e.UserId == "user-123" &&
                    e.Email == "player@test.com" &&
                    e.GameId == "game-123" &&
                    e.GameName == "Halo" &&
                    e.GameValue == 99.90m &&
                    e.Amount == 1 &&
                    e.EventType == "PURCHASE_REQUESTED" &&
                    e.RequestedAt != default)),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Search_WhenTermIsEmpty_ReturnsEmptyListWithoutCallingSearchRepository(string? term)
    {
        var searchRepository = new Mock<IGameSearchRepository>();
        var service = CreateService(searchRepository: searchRepository);

        var result = await service.Search(term!);

        Assert.Empty(result);
        searchRepository.Verify(
            r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task Search_WhenPageAndPageSizeAreInvalid_NormalizesValuesBeforeSearching()
    {
        var expected = new List<Game>
        {
            new() { Id = "game-123", Name = "Halo", Category = "FPS", Price = 99.90m }
        };

        var searchRepository = new Mock<IGameSearchRepository>();
        searchRepository
            .Setup(r => r.SearchAsync("halo", 1, 10))
            .ReturnsAsync(expected);

        var service = CreateService(searchRepository: searchRepository);

        var result = await service.Search("halo", page: -5, pageSize: 0);

        Assert.Same(expected, result);
        searchRepository.Verify(r => r.SearchAsync("halo", 1, 10), Times.Once);
    }

    [Fact]
    public async Task Search_WhenPageAndPageSizeAreValid_ForwardsValuesToSearchRepository()
    {
        var expected = new List<Game>
        {
            new() { Id = "game-123", Name = "Halo", Category = "FPS", Price = 99.90m }
        };

        var searchRepository = new Mock<IGameSearchRepository>();
        searchRepository
            .Setup(r => r.SearchAsync("halo", 2, 25))
            .ReturnsAsync(expected);

        var service = CreateService(searchRepository: searchRepository);

        var result = await service.Search("halo", page: 2, pageSize: 25);

        Assert.Same(expected, result);
        searchRepository.Verify(r => r.SearchAsync("halo", 2, 25), Times.Once);
    }

    [Fact]
    public async Task GetAllCached_WhenCacheHasGames_ReturnsGamesFromDistributedCache()
    {
        var cachedGames = new List<Game>
        {
            new() { Id = "game-123", Name = "Halo", Category = "FPS", Price = 99.90m }
        };

        var cache = new InMemoryDistributedCache();
        await cache.SetStringAsync("games:list", JsonSerializer.Serialize(cachedGames));

        var service = CreateService(cache: cache);

        var result = await service.GetAllCached();

        Assert.Single(result);
        Assert.Equal("game-123", result[0].Id);
        Assert.Equal("Halo", result[0].Name);
    }

    private static GameService CreateService(
        Mock<IMessagePublisher>? publisher = null,
        Mock<IGameSearchRepository>? searchRepository = null,
        IDistributedCache? cache = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DynamoDb:GamesTable"] = "Games",
                ["RabbitMq:PaymentQueueName"] = "custom-payment-queue"
            })
            .Build();

        return new GameService(
            Mock.Of<IAmazonDynamoDB>(),
            publisher?.Object ?? Mock.Of<IMessagePublisher>(),
            configuration,
            cache ?? Mock.Of<IDistributedCache>(),
            searchRepository?.Object ?? Mock.Of<IGameSearchRepository>());
    }

    private sealed class InMemoryDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _items = new();

        public byte[]? Get(string key) => _items.GetValueOrDefault(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _items[key] = value;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _items.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }
}
