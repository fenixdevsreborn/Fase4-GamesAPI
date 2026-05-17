using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ms_games.Models;
using ms_games.Services;
using System.Security.Claims;

namespace ms_games.Tests;

public class GamesControllerTests
{
    [Fact]
    public async Task BuyGame_WhenUserClaimsAreValid_ReturnsAcceptedAndRequestsPurchase()
    {
        var game = new Game
        {
            Id = "game-123",
            Name = "Halo",
            Price = 99.90m,
            Category = "FPS"
        };

        var service = new Mock<IGameService>();
        service.Setup(s => s.GetById("game-123")).ReturnsAsync(game);

        var controller = CreateController(service, userId: "user-123", email: "player@test.com");

        var result = await controller.BuyGame("game-123", new BuyRequest { Amount = 2 });

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(accepted.Value);

        service.Verify(s => s.RequestPurchase(
            "user-123",
            "player@test.com",
            "game-123",
            "Halo",
            99.90m,
            2), Times.Once);
    }

    [Fact]
    public async Task GetCached_ReturnsCachedGamesFromService()
    {
        var games = new List<Game>
        {
            new() { Id = "game-123", Name = "Halo", Category = "FPS", Price = 99.90m }
        };

        var service = new Mock<IGameService>();
        service.Setup(s => s.GetAllCached()).ReturnsAsync(games);

        var controller = CreateController(service, userId: null, email: null);

        var result = await controller.GetCached();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(games, ok.Value);
        service.Verify(s => s.GetAllCached(), Times.Once);
    }

    [Fact]
    public async Task BuyGame_WhenUserIdClaimIsMissing_ReturnsUnauthorized()
    {
        var service = new Mock<IGameService>();
        var controller = CreateController(service, userId: null, email: "player@test.com");

        var result = await controller.BuyGame("game-123", new BuyRequest { Amount = 1 });

        Assert.IsType<UnauthorizedResult>(result);
        service.Verify(s => s.GetById(It.IsAny<string>()), Times.Never);
        service.Verify(s => s.RequestPurchase(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task GetRecommendations_WhenUserClaimsAreValid_ReturnsRecommendations()
    {
        var recommendations = new List<Game>
        {
            new() { Id = "game-456", Name = "Doom", Category = "FPS", Price = 79.90m }
        };

        var service = new Mock<IGameService>();
        service.Setup(s => s.GetRecommendation("game-123", 3)).ReturnsAsync(recommendations);

        var controller = CreateController(service, userId: "user-123", email: "player@test.com");

        var result = await controller.GetRecommendations("game-123", 3);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(recommendations, ok.Value);
        service.Verify(s => s.GetRecommendation("game-123", 3), Times.Once);
    }

    private static GamesController CreateController(
        Mock<IGameService> service,
        string? userId,
        string? email)
    {
        var claims = new List<Claim>();

        if (userId is not null)
        {
            claims.Add(new Claim("sub", userId));
        }

        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        var controller = new GamesController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };

        return controller;
    }
}
