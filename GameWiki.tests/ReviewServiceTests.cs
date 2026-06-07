using GameWiki.DTOs.Review;
using GameWiki.Models;
using GameWiki.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GameWiki.Tests;

public class ReviewServiceTests
{
    private GameWikiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameWikiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GameWikiDbContext(options);
    }

    private ReviewService CreateService(GameWikiDbContext db)
    {
        var mockNotifications = new Mock<NotificationService>(db);
        return new ReviewService(db, mockNotifications.Object);
    }

    private async Task<(User user, Game game)> SeedAsync(GameWikiDbContext db)
    {
        var user = new User { Username = "TestUser", Email = "test@test.com", PasswordHash = "hash" };
        var game = new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow };
        db.Users.Add(user);
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return (user, game);
    }

    [Fact]
    public async Task GetReviewsAsync_ReturnsReviewsForGame()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        db.Reviews.Add(new Review { GameId = game.Id, UserId = user.Id, Rating = 8, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetReviewsAsync(game.Id, null);

        Assert.Single(result);
        Assert.Equal(8, result[0].Rating);
    }

    [Fact]
    public async Task GetReviewsAsync_NonExistingGame_ReturnsEmptyList()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var result = await service.GetReviewsAsync(999, null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetExistingReviewAsync_UserHasReview_ReturnsIt()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        db.Reviews.Add(new Review { GameId = game.Id, UserId = user.Id, Rating = 7, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetExistingReviewAsync(game.Id, user.Id);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetExistingReviewAsync_UserHasNoReview_ReturnsNull()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var service = CreateService(db);

        var result = await service.GetExistingReviewAsync(game.Id, user.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_WithoutContent_IsVerifiedAutomatically()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var service = CreateService(db);
        var dto = new CreateReviewDto { GameId = game.Id, Rating = 9, Content = "" };

        var result = await service.CreateAsync(game.Id, user.Id, dto, isMod: false);

        Assert.True(result.IsVerified);
    }

    [Fact]
    public async Task CreateAsync_WithContentByMod_IsVerifiedAutomatically()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var service = CreateService(db);
        var dto = new CreateReviewDto { GameId = game.Id, Rating = 9, Content = "Świetna gra!" };

        var result = await service.CreateAsync(game.Id, user.Id, dto, isMod: true);

        Assert.True(result.IsVerified);
    }

    [Fact]
    public async Task CreateAsync_WithContentByUser_RequiresVerification()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var service = CreateService(db);
        var dto = new CreateReviewDto { GameId = game.Id, Rating = 9, Content = "Świetna gra!" };

        var result = await service.CreateAsync(game.Id, user.Id, dto, isMod: false);

        Assert.False(result.IsVerified);
    }

    [Fact]
    public async Task CreateAsync_SavesReviewToDatabase()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var service = CreateService(db);
        var dto = new CreateReviewDto { GameId = game.Id, Rating = 7, Content = "" };

        await service.CreateAsync(game.Id, user.Id, dto, isMod: false);

        Assert.Equal(1, await db.Reviews.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_RemovesReviewFromDatabase()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var review = new Review { GameId = game.Id, UserId = user.Id, Rating = 5, CreatedAt = DateTime.UtcNow };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.DeleteAsync(review);

        Assert.Equal(0, await db.Reviews.CountAsync());
    }

    [Fact]
    public async Task VerifyAsync_ExistingReview_SetsIsVerifiedTrue()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var review = new Review { GameId = game.Id, UserId = user.Id, Rating = 8, IsVerified = false, CreatedAt = DateTime.UtcNow };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.VerifyAsync(review.Id);

        Assert.NotNull(result);
        Assert.True(result.IsVerified);
    }

    [Fact]
    public async Task VerifyAsync_NonExistingReview_ReturnsNull()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var result = await service.VerifyAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyUnverifiedWithContent()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        db.Reviews.AddRange(
            new Review { GameId = game.Id, UserId = user.Id, Rating = 8, Content = "Dobra gra", IsVerified = false, CreatedAt = DateTime.UtcNow },
            new Review { GameId = game.Id, UserId = user.Id, Rating = 9, Content = "", IsVerified = false, CreatedAt = DateTime.UtcNow },
            new Review { GameId = game.Id, UserId = user.Id, Rating = 7, Content = "Inna recenzja", IsVerified = true, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetPendingAsync();

        Assert.Single(result);
        Assert.Equal("Dobra gra", result[0].Content);
    }
}