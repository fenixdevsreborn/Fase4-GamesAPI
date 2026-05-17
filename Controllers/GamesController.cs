using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ms_games.Models;
using ms_games.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

[ApiController]
[Route("games")]
public class GamesController : ControllerBase
{
  private readonly IGameService _service;

  public GamesController(IGameService service)
  {
    _service = service;
  }

  [HttpGet]
  public async Task<IActionResult> Get()
  {
    return Ok(await _service.GetAll());
  }

  [HttpGet("cache")]
  public async Task<IActionResult> GetCached()
  {
    return Ok(await _service.GetAllCached());
  }

  [HttpGet("{id}")]
  public async Task<IActionResult> GetById(string id)
  {
    return Ok(await _service.GetById(id));
  }
  
  [HttpGet("/search")]
  public async Task<IActionResult> Search(
      [FromQuery] string q,
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 10)
  {
      var result = await _service.Search(q, page, pageSize);
      return Ok(result);
  }

  [HttpPost]
  public async Task<IActionResult> Create([FromBody] Game game)
  {
    await _service.Create(game);
    return Ok(game);
  }

  [HttpPut("{id}")]
  public async Task<IActionResult> Update(string id, [FromBody] Game game)
  {
    await _service.Update(id, game);
    return Ok();
  }

  [HttpDelete("{id}")]
  public async Task<IActionResult> Delete(string id)
  {
    await _service.Delete(id);
    return Ok();
  }

  [Authorize]
  [HttpPost("{gameId}/buy")]
  public async Task<IActionResult> BuyGame(string gameId, [FromBody] BuyRequest request)
  {
    var userId = GetUserId();
    var email = GetUserEmail();

    if (userId == null || email == null)
      return Unauthorized();

    var game = await _service.GetById(gameId);

    await _service.RequestPurchase(
      userId,
      email,
      gameId,
      game.Name,
      game.Price,
      request.Amount
    );

    return Accepted(new
    {
      Message = "Purchase request sent",
      GameId = gameId,
      Amount = request.Amount
    });
  }

  [Authorize]
  [HttpGet("{gameId}/recommendations")]
  public async Task<IActionResult> GetRecommendations(string gameId, [FromQuery] int count = 3)
  {
    var userId = GetUserId();
    var email = GetUserEmail();

    if (userId == null || email == null)
      return Unauthorized();

    var recommendations = await _service.GetRecommendation(gameId, count);

    return Ok(recommendations);
  }

  private string? GetUserId()
  {
    return User.FindFirst("sub")?.Value
      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
      ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
  }

  private string? GetUserEmail()
  {
    return User.FindFirst("email")?.Value
      ?? User.FindFirst(ClaimTypes.Email)?.Value
      ?? User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
  }
}
