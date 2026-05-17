namespace ms_games.Events
{
  public class PurchaseRequestedEvent
  {
    public string EventType => "PURCHASE_REQUESTED";

    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string GameId { get; set; } = string.Empty;

    public decimal GameValue{ get; set; }

    public string GameName{ get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime RequestedAt { get; set; }
  }
}
