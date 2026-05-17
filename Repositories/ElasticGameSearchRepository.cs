using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ms_games.Models;

namespace ms_games.Repositories
{
    public interface IGameSearchRepository
    {
        Task IndexAsync(Game game);
        Task<List<Game>> SearchAsync(string term, int page, int pageSize);
        Task DeleteAsync(string id);
    }

    public class ElasticGameSearchRepository : IGameSearchRepository
    {
        private readonly ElasticsearchClient _client;
        private readonly ILogger<ElasticGameSearchRepository> _logger;
        private readonly string _indexName;

        public ElasticGameSearchRepository(IConfiguration configuration, ILogger<ElasticGameSearchRepository> logger)
        {
            var url = configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
            _indexName = configuration["Elasticsearch:IndexName"] ?? "games";
            _logger = logger;

            var settings = new ElasticsearchClientSettings(new Uri(url))
                .DefaultIndex(_indexName);

            _client = new ElasticsearchClient(settings);
        }

        public async Task IndexAsync(Game game)
        {
            var response = await _client.IndexAsync(game, request => request
                .Index(_indexName)
                .Id(game.Id)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogError(
                    "Failed to index game {GameId} in Elasticsearch. Error: {Error}",
                    game.Id,
                    response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);

                throw new InvalidOperationException("Failed to index game in Elasticsearch.");
            }
        }

        public async Task<List<Game>> SearchAsync(string term, int page, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new List<Game>();

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var from = (page - 1) * pageSize;

            var response = await _client.SearchAsync<Game>(request => request
                .Indices(_indexName)
                .From(from)
                .Size(pageSize)
                .Query(query => query
                    .MultiMatch(multiMatch => multiMatch
                        .Query(term)
                        .Fields(new[] { "name^3", "description", "category^2" })
                        .Fuzziness(new Fuzziness("AUTO"))
                    )
                )
                .Sort(sort => sort
                    .Score(new ScoreSort { Order = SortOrder.Desc })
                )
            );

            if (!response.IsValidResponse)
            {
                _logger.LogError(
                    "Failed to search games in Elasticsearch. Term: {Term}. Error: {Error}",
                    term,
                    response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);

                throw new InvalidOperationException("Failed to search games in Elasticsearch.");
            }

            return response.Documents.ToList();
        }

        public async Task DeleteAsync(string id)
        {
            var response = await _client.DeleteAsync<Game>(id, request => request.Index(_indexName));

            if (!response.IsValidResponse && response.ApiCallDetails.HttpStatusCode != 404)
            {
                _logger.LogError(
                    "Failed to delete game {GameId} from Elasticsearch. Error: {Error}",
                    id,
                    response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);

                throw new InvalidOperationException("Failed to delete game from Elasticsearch.");
            }
        }
    }
}
