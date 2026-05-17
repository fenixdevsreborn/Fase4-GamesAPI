using Amazon.DynamoDBv2.DataModel;

namespace ms_games.Models
{
    [DynamoDBTable("Games")]
    public class Game
    {
      [DynamoDBHashKey]
      public string Id { get; set; } = Guid.NewGuid().ToString();
      public string Name { get; set; } = string.Empty;
      public decimal Price { get; set; }
      public string Description { get; set; } = string.Empty;
      public string Category { get; set; } = string.Empty;
    }
}
