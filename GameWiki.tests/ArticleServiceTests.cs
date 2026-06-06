using GameWiki.DTOs.Article;
using GameWiki.Models;
using GameWiki.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.AspNetCore.Hosting;

namespace GameWiki.Tests;

public class ArticleServiceTests
{
    private GameWikiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameWikiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new GameWikiDbContext(options);
    }

    private ArticleService CreateService(GameWikiDbContext db)
    {
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());
        return new ArticleService(db, mockEnv.Object);
    }

    private async Task<(User user, Game game)> SeedUserAndGameAsync(GameWikiDbContext db)
    {
        var user = new User { Username = "TestUser", Email = "test@test.com", PasswordHash = "hash" };
        var game = new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow };
        db.Users.Add(user);
        db.Games.Add(game);
        await db.SaveChangesAsync();
        return (user, game);
    }
    [Fact]
    public async Task GetArticlesAsync_NoFilters_ReturnsAllArticles()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        db.Articles.AddRange(
            new Article { Title = "Artykuł 1", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow },
            new Article { Title = "Artykuł 2", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var (articles, _) = await service.GetArticlesAsync(null, null);

        Assert.Equal(2, articles.Count);
    }

    [Fact]
    public async Task GetArticlesAsync_WithGameId_ReturnsOnlyMatchingArticles()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var game2 = new Game { Title = "Cyberpunk", Description = "FPS", ReleaseDate = DateTime.UtcNow };
        db.Games.Add(game2);
        await db.SaveChangesAsync();

        db.Articles.AddRange(
            new Article { Title = "O Wiedźminie", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow },
            new Article { Title = "O Cyberpunku", GameId = game2.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var (articles, selectedTitle) = await service.GetArticlesAsync(game.Id, null);

        Assert.Single(articles);
        Assert.Equal("O Wiedźminie", articles[0].Title);
        Assert.Equal("Wiedźmin 3", selectedTitle);
    }

    [Fact]
    public async Task GetArticlesAsync_WithGameSearch_ReturnsMatchingArticles()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var game2 = new Game { Title = "Cyberpunk", Description = "FPS", ReleaseDate = DateTime.UtcNow };
        db.Games.Add(game2);
        await db.SaveChangesAsync();

        db.Articles.AddRange(
            new Article { Title = "O Wiedźminie", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow },
            new Article { Title = "O Cyberpunku", GameId = game2.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var (articles, _) = await service.GetArticlesAsync(null, "Cyber");

        Assert.Single(articles);
        Assert.Equal("O Cyberpunku", articles[0].Title);
    }

    [Fact]
    public async Task GetArticlesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var (articles, _) = await service.GetArticlesAsync(null, null);

        Assert.Empty(articles);
    }
    [Fact]
    public async Task GetArticleWithDetailsAsync_ExistingId_ReturnsArticle()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var article = new Article { Title = "Test", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetArticleWithDetailsAsync(article.Id);

        Assert.NotNull(result);
        Assert.Equal("Test", result.Title);
    }

    [Fact]
    public async Task GetArticleWithDetailsAsync_NonExistingId_ReturnsNull()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var result = await service.GetArticleWithDetailsAsync(999);

        Assert.Null(result);
    }
    [Fact]
    public async Task CreateArticleAsync_WithoutCoverImage_SavesArticleToDatabase()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var service = CreateService(db);
        var dto = new CreateArticleDto { GameId = game.Id, Title = "Nowy artykuł", CoverImage = null };

        var result = await service.CreateArticleAsync(dto, user.Id, isVerified: false);

        Assert.NotNull(result);
        Assert.Equal("Nowy artykuł", result.Title);
        Assert.Equal(user.Id, result.AuthorId);
        Assert.False(result.IsVerified);
        Assert.Equal(1, await db.Articles.CountAsync());
    }

    [Fact]
    public async Task CreateArticleAsync_AsVerified_SetsIsVerifiedTrue()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var service = CreateService(db);
        var dto = new CreateArticleDto { GameId = game.Id, Title = "Artykuł moderatora", CoverImage = null };

        var result = await service.CreateArticleAsync(dto, user.Id, isVerified: true);

        Assert.True(result.IsVerified);
    }

    [Fact]
    public async Task CreateArticleAsync_WithTextBlocks_SavesBlocksToDatabase()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var service = CreateService(db);
        var dto = new CreateArticleDto
        {
            GameId = game.Id,
            Title = "Artykuł z blokami",
            CoverImage = null,
            Blocks = new List<BlockInputDto>
            {
                new BlockInputDto { Type = ArticleBlockType.Text, TextContent = "Pierwszy akapit" },
                new BlockInputDto { Type = ArticleBlockType.Text, TextContent = "Drugi akapit" }
            }
        };

        await service.CreateArticleAsync(dto, user.Id, isVerified: false);

        Assert.Equal(2, await db.ArticleBlocks.CountAsync());
    }
    [Fact]
    public async Task DeleteArticleAsync_RemovesArticleFromDatabase()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var article = new Article { Title = "Do usunięcia", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.DeleteArticleAsync(article, moderatorId: null, reason: null);

        Assert.Equal(0, await db.Articles.CountAsync());
    }

    [Fact]
    public async Task DeleteArticleAsync_ByModerator_CreatesNotificationForAuthor()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var moderator = new User { Username = "Mod", Email = "mod@test.com", PasswordHash = "hash" };
        db.Users.Add(moderator);
        var article = new Article { Title = "Artykuł", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.DeleteArticleAsync(article, moderatorId: moderator.Id, reason: "Spam");

        var notification = await db.UserNotifications.FirstOrDefaultAsync(n => n.UserId == user.Id);
        Assert.NotNull(notification);
        Assert.Equal("Spam", notification.Reason);
    }

    [Fact]
    public async Task DeleteArticleAsync_ByAuthorHimself_DoesNotCreateNotification()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var article = new Article { Title = "Artykuł", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.DeleteArticleAsync(article, moderatorId: user.Id, reason: null);

        Assert.Equal(0, await db.UserNotifications.CountAsync());
    }
    [Fact]
    public async Task VerifyArticleAsync_ExistingArticle_SetsIsVerifiedTrue()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var article = new Article { Title = "Artykuł", GameId = game.Id, AuthorId = user.Id, IsVerified = false, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.VerifyArticleAsync(article.Id);

        Assert.NotNull(result);
        Assert.True(result.IsVerified);
    }

    [Fact]
    public async Task VerifyArticleAsync_NonExistingArticle_ReturnsNull()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var result = await service.VerifyArticleAsync(999);

        Assert.Null(result);
    }
    [Fact]
    public async Task AddCommentAsync_SavesCommentToDatabase()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var article = new Article { Title = "Artykuł", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.AddCommentAsync(article.Id, user.Id, "  Świetny artykuł!  ", parentCommentId: null);

        var comment = await db.Comments.FirstOrDefaultAsync();
        Assert.NotNull(comment);
        Assert.Equal("Świetny artykuł!", comment.Content); // sprawdza też że Trim() zadziałał
        Assert.Equal(article.Id, comment.ArticleId);
    }
    [Fact]
    public async Task ReactAsync_NewReaction_AddsReactionToDatabase()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var article = new Article { Title = "Artykuł", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        var comment = new Comment { ArticleId = article.Id, UserId = user.Id, Content = "Komentarz", CreatedAt = DateTime.UtcNow };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ReactAsync(comment.Id, user.Id, ReactionType.Like);

        Assert.Equal(1, await db.CommentReactions.CountAsync());
    }

    [Fact]
    public async Task ReactAsync_SameReactionTwice_RemovesReaction()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var article = new Article { Title = "Artykuł", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        var comment = new Comment { ArticleId = article.Id, UserId = user.Id, Content = "Komentarz", CreatedAt = DateTime.UtcNow };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ReactAsync(comment.Id, user.Id, ReactionType.Like);
        await service.ReactAsync(comment.Id, user.Id, ReactionType.Like);

        Assert.Equal(0, await db.CommentReactions.CountAsync());
    }

    [Fact]
    public async Task ReactAsync_DifferentReaction_ChangesReactionType()
    {
        var db = CreateDb();
        var (user, game) = await SeedUserAndGameAsync(db);
        var article = new Article { Title = "Artykuł", GameId = game.Id, AuthorId = user.Id, CreatedAt = DateTime.UtcNow };
        db.Articles.Add(article);
        var comment = new Comment { ArticleId = article.Id, UserId = user.Id, Content = "Komentarz", CreatedAt = DateTime.UtcNow };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ReactAsync(comment.Id, user.Id, ReactionType.Like);
        await service.ReactAsync(comment.Id, user.Id, ReactionType.Dislike);

        Assert.Equal(1, await db.CommentReactions.CountAsync());
        var reaction = await db.CommentReactions.FirstAsync();
        Assert.Equal(ReactionType.Dislike, reaction.Type);
    }
}