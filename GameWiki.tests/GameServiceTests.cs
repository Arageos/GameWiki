using GameWiki.DTOs.Game;
using GameWiki.Models;
using GameWiki.Services;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Tests;

public class GameServiceTests
{
    // Pomocnicza metoda — tworzy świeżą bazę "w pamięci" dla każdego testu
    private GameWikiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameWikiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // nowa baza per test
            .Options;
        return new GameWikiDbContext(options);
    }

    [Fact] // <-- ta adnotacja mówi xUnit że to jest test
    public async Task GetFilteredGamesAsync_NoFilters_ReturnsAllGames()
    {
        // ARRANGE — przygotuj dane
        var db = CreateDb();
        db.Games.AddRange(
            new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow },
            new Game { Title = "Cyberpunk 2077", Description = "FPS RPG", ReleaseDate = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var service = new GameService(db);

        // ACT — wywołaj testowaną metodę
        var result = await service.GetFilteredGamesAsync(null, null, null);

        // ASSERT — sprawdź wynik
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetFilteredGamesAsync_WithSearch_ReturnsMatchingGames()
    {
        var db = CreateDb();
        db.Games.AddRange(
            new Game { Title = "Wiedźmin 3", Description = "RPG", ReleaseDate = DateTime.UtcNow },
            new Game { Title = "Cyberpunk 2077", Description = "FPS RPG", ReleaseDate = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var service = new GameService(db);

        var result = await service.GetFilteredGamesAsync("Wiedźmin", null, null);

        Assert.Single(result); // czyli dokładnie 1 wynik
        Assert.Equal("Wiedźmin 3", result[0].Title);
    }
}