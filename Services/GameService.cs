using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.Extensions.Caching.Distributed;
using ms_games.Events;
using ms_games.Messaging;
using ms_games.Models;
using ms_games.Repositories;
using System.Text.Json;

namespace ms_games.Services
{
  public interface IGameService
  {
    Task<List<Game>> GetAll();
    Task<List<Game>> GetAllCached();
    Task<Game> GetById(string id);
    Task<List<Game>> Search(string term, int page = 1, int pageSize = 10);
    Task<List<Game>> GetRecommendation(string gameId, int limit = 5);
    Task Create(Game game);
    Task Update(string id, Game game);
    Task Delete(string id);
    Task RequestPurchase(string userId, string email, string gameId, string gameName, decimal gameValue, decimal amount);
  }

  public class GameService : IGameService
  {
    private readonly DynamoDBContext _context;
    private readonly IMessagePublisher _publisher;
    private readonly string _table;
    private readonly string _paymentQueueName;
    private readonly IDistributedCache _cache;
    private readonly IGameSearchRepository _searchRepository;
    private readonly string _gamesCacheKey;
    private readonly int _gamesCacheTtlSeconds;

    public GameService(
      IAmazonDynamoDB dynamo,
      IMessagePublisher publisher,
      IConfiguration configuration,
      IDistributedCache cache,
      IGameSearchRepository searchRepository)
    {
      _context = new DynamoDBContextBuilder()
        .WithDynamoDBClient(() => dynamo)
        .Build();
      _publisher = publisher;
      _cache = cache;
      _table = configuration["DynamoDb:GamesTable"] ?? Environment.GetEnvironmentVariable("GAMES_TABLE") ?? "Games";
      _paymentQueueName = configuration["RabbitMq:PaymentQueueName"] ?? "payment-queue";
      _gamesCacheKey = configuration["Cache:GamesListKey"] ?? "games:list";
      _gamesCacheTtlSeconds = int.TryParse(configuration["Cache:GamesListTtlSeconds"], out var ttlSeconds)
        ? ttlSeconds
        : 300;
      _searchRepository = searchRepository;
    }

    public async Task<List<Game>> GetAll()
    {
      return await _context.ScanAsync<Game>(new List<ScanCondition>(), ScanConfig()).GetRemainingAsync();
    }

    public async Task<List<Game>> GetAllCached()
    {
      var cachedGames = await _cache.GetStringAsync(_gamesCacheKey);

      if (!string.IsNullOrWhiteSpace(cachedGames))
      {
        return JsonSerializer.Deserialize<List<Game>>(cachedGames) ?? new List<Game>();
      }

      var games = await GetAll();

      await _cache.SetStringAsync(
        _gamesCacheKey,
        JsonSerializer.Serialize(games),
        new DistributedCacheEntryOptions
        {
          AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_gamesCacheTtlSeconds)
        });

      return games;
    }

    public async Task<Game> GetById(string id)
    {
      return await _context.LoadAsync<Game>(id, LoadConfig());
    }

    public async Task<List<Game>> Search(string term, int page = 1, int pageSize = 10)
    {
      if (string.IsNullOrWhiteSpace(term))
        return new List<Game>();

      if (page < 1)
        page = 1;

      if (pageSize < 1)
        pageSize = 10;

      return await _searchRepository.SearchAsync(term, page, pageSize);
    }

    public async Task<List<Game>> GetRecommendation(string gameId, int limit = 5)
    {
      var baseGame = await _context.LoadAsync<Game>(gameId, LoadConfig());

      if (baseGame == null)
        throw new Exception("Game not found");

      var conditions = new List<ScanCondition>
      {
        new ScanCondition("Category", ScanOperator.Equal, baseGame.Category)
      };

      var games = await _context
        .ScanAsync<Game>(conditions, ScanConfig())
        .GetRemainingAsync();

      var recommendations = games
        .Where(g => g.Id != gameId)
        .OrderBy(g => Math.Abs(g.Price - baseGame.Price))
        .ThenBy(g => g.Name)
        .Take(limit)
        .ToList();

      return recommendations;
    }

    public async Task Create(Game game)
    {
      game.Id = Guid.NewGuid().ToString();
      await _context.SaveAsync(game, SaveConfig());
      await _searchRepository.IndexAsync(game);
      await _cache.RemoveAsync(_gamesCacheKey);
    }

    public async Task Update(string id, Game game)
    {
      game.Id = id;
      await _context.SaveAsync(game, SaveConfig());
      await _searchRepository.IndexAsync(game);
      await _cache.RemoveAsync(_gamesCacheKey);
    }

    public async Task Delete(string id)
    {
      await _context.DeleteAsync<Game>(id, DeleteConfig());
      await _searchRepository.DeleteAsync(id);
      await _cache.RemoveAsync(_gamesCacheKey);
    }

    public async Task RequestPurchase(string userId, string email, string gameId, string gameName, decimal gameValue, decimal amount)
    {
      var evt = new PurchaseRequestedEvent
      {
        UserId = userId,
        Email = email,
        GameId = gameId,
        GameName = gameName,
        GameValue = gameValue,
        Amount = amount,
        RequestedAt = DateTime.UtcNow
      };

      await _publisher.PublishAsync(_paymentQueueName, evt);
    }

    private LoadConfig LoadConfig() => new()
    {
      OverrideTableName = _table
    };

    private SaveConfig SaveConfig() => new()
    {
      OverrideTableName = _table
    };

    private DeleteConfig DeleteConfig() => new()
    {
      OverrideTableName = _table
    };

    private ScanConfig ScanConfig() => new()
    {
      OverrideTableName = _table
    };
  }
}
