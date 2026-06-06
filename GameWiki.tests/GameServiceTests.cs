using GameWiki.Models;
using GameWiki.Services;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Tests;

public class GameServiceTests
{
    private GameWikiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameWikiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new GameWikiDbContext(options);
    }

    // ── GetFilteredGamesAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetFilteredGamesAsync_NoFilters_ReturnsAllGames()
    {
        // ARRANGE
        var db = CreateDb();
        db.Games.AddRange(
            new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow },
            new Game { Title = "Cyberpunk 2077", Description = "FPS RPG", ReleaseDate = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = new GameService(db);

        // ACT
        var result = await service.GetFilteredGamesAsync(null, null, null);

        // ASSERT
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetFilteredGamesAsync_WithSearch_ReturnsMatchingGames()
    {
        // ARRANGE
        var db = CreateDb();
        db.Games.AddRange(
            new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow },
            new Game { Title = "Cyberpunk 2077", Description = "FPS RPG", ReleaseDate = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = new GameService(db);

        // ACT
        var result = await service.GetFilteredGamesAsync("Wiedźmin", null, null);

        // ASSERT
        Assert.Single(result);
        Assert.Equal("Wiedźmin 3", result[0].Title);
    }

    [Fact]
    public async Task GetFilteredGamesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // ARRANGE
        var db = CreateDb();
        var service = new GameService(db);

        // ACT
        var result = await service.GetFilteredGamesAsync(null, null, null);

        // ASSERT
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFilteredGamesAsync_SearchNoMatch_ReturnsEmptyList()
    {
        // ARRANGE
        var db = CreateDb();
        db.Games.Add(new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new GameService(db);

        // ACT
        var result = await service.GetFilteredGamesAsync("NieIstniejącaGra", null, null);

        // ASSERT
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFilteredGamesAsync_WithGenreFilter_ReturnsOnlyMatchingGames()
    {
        // ARRANGE
        var db = CreateDb();

        var rpg = new Genre { Name = "RPG" };
        var fps = new Genre { Name = "FPS" };
        db.Genres.AddRange(rpg, fps);

        var witcher = new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow };
        var doom = new Game { Title = "Doom", Description = "FPS", ReleaseDate = DateTime.UtcNow };
        db.Games.AddRange(witcher, doom);
        await db.SaveChangesAsync();

        db.GameGenres.Add(new GameGenre { GameId = witcher.Id, GenreId = rpg.Id });
        db.GameGenres.Add(new GameGenre { GameId = doom.Id, GenreId = fps.Id });
        await db.SaveChangesAsync();

        var service = new GameService(db);

        // ACT
        var result = await service.GetFilteredGamesAsync(null, rpg.Id, null);

        // ASSERT
        Assert.Single(result);
        Assert.Equal("Wiedźmin 3", result[0].Title);
    }

    [Fact]
    public async Task GetFilteredGamesAsync_WithPlatformFilter_ReturnsOnlyMatchingGames()
    {
        // ARRANGE
        var db = CreateDb();

        var pc = new Platform { Name = "PC" };
        var ps5 = new Platform { Name = "PS5" };
        db.Platforms.AddRange(pc, ps5);

        var witcher = new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow };
        var spiderman = new Game { Title = "Spider-Man", Description = "Action", ReleaseDate = DateTime.UtcNow };
        db.Games.AddRange(witcher, spiderman);
        await db.SaveChangesAsync();

        db.GamePlatforms.Add(new GamePlatform { GameId = witcher.Id, PlatformId = pc.Id });
        db.GamePlatforms.Add(new GamePlatform { GameId = spiderman.Id, PlatformId = ps5.Id });
        await db.SaveChangesAsync();

        var service = new GameService(db);

        // ACT
        var result = await service.GetFilteredGamesAsync(null, null, ps5.Id);

        // ASSERT
        Assert.Single(result);
        Assert.Equal("Spider-Man", result[0].Title);
    }

    // ── GetGameByIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetGameByIdAsync_ExistingId_ReturnsGame()
    {
        // ARRANGE
        var db = CreateDb();
        var game = new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow };
        db.Games.Add(game);
        await db.SaveChangesAsync();
        var service = new GameService(db);

        // ACT
        var result = await service.GetGameByIdAsync(game.Id);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal("Wiedźmin 3", result.Title);
    }

    [Fact]
    public async Task GetGameByIdAsync_NonExistingId_ReturnsNull()
    {
        // ARRANGE
        var db = CreateDb();
        var service = new GameService(db);

        // ACT
        var result = await service.GetGameByIdAsync(999);

        // ASSERT
        Assert.Null(result);
    }
}