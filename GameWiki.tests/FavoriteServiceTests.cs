using GameWiki.Models;
using GameWiki.Services;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Tests;

public class FavoriteServiceTests
{
    private GameWikiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameWikiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GameWikiDbContext(options);
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
    public async Task CreateListAsync_NewList_ReturnsTrue()
    {
        var db = CreateDb();
        var (user, _) = await SeedAsync(db);
        var service = new FavoriteService(db);

        var result = await service.CreateListAsync(user.Id, "Ulubione");

        Assert.True(result);
        Assert.Equal(1, await db.FavoriteLists.CountAsync());
    }

    [Fact]
    public async Task CreateListAsync_DuplicateName_ReturnsFalse()
    {
        var db = CreateDb();
        var (user, _) = await SeedAsync(db);
        db.FavoriteLists.Add(new FavoriteList { UserId = user.Id, Name = "Ulubione" });
        await db.SaveChangesAsync();
        var service = new FavoriteService(db);

        var result = await service.CreateListAsync(user.Id, "Ulubione");

        Assert.False(result);
        Assert.Equal(1, await db.FavoriteLists.CountAsync());
    }

    [Fact]
    public async Task CreateListAsync_SameNameDifferentUser_ReturnsTrue()
    {
        var db = CreateDb();
        var (user, _) = await SeedAsync(db);
        var user2 = new User { Username = "User2", Email = "user2@test.com", PasswordHash = "hash" };
        db.Users.Add(user2);
        db.FavoriteLists.Add(new FavoriteList { UserId = user.Id, Name = "Ulubione" });
        await db.SaveChangesAsync();
        var service = new FavoriteService(db);

        var result = await service.CreateListAsync(user2.Id, "Ulubione");

        Assert.True(result);
    }

    [Fact]
    public async Task AddToFavoriteAsync_ValidListAndGame_ReturnsTrue()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var list = new FavoriteList { UserId = user.Id, Name = "Ulubione" };
        db.FavoriteLists.Add(list);
        await db.SaveChangesAsync();
        var service = new FavoriteService(db);

        var result = await service.AddToFavoriteAsync(user.Id, game.Id, list.Id);

        Assert.True(result);
        Assert.Equal(1, await db.FavoriteGames.CountAsync());
    }

    [Fact]
    public async Task AddToFavoriteAsync_AlreadyAdded_ReturnsFalse()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var list = new FavoriteList { UserId = user.Id, Name = "Ulubione" };
        db.FavoriteLists.Add(list);
        await db.SaveChangesAsync();
        db.FavoriteGames.Add(new FavoriteGame { FavoriteListId = list.Id, GameId = game.Id });
        await db.SaveChangesAsync();
        var service = new FavoriteService(db);

        var result = await service.AddToFavoriteAsync(user.Id, game.Id, list.Id);

        Assert.False(result);
        Assert.Equal(1, await db.FavoriteGames.CountAsync());
    }

    [Fact]
    public async Task AddToFavoriteAsync_ListBelongsToOtherUser_ReturnsFalse()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var user2 = new User { Username = "User2", Email = "user2@test.com", PasswordHash = "hash" };
        db.Users.Add(user2);
        var list = new FavoriteList { UserId = user2.Id, Name = "Ulubione" };
        db.FavoriteLists.Add(list);
        await db.SaveChangesAsync();
        var service = new FavoriteService(db);

        var result = await service.AddToFavoriteAsync(user.Id, game.Id, list.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveFromFavoriteAsync_ExistingEntry_RemovesAndReturnsTrue()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var list = new FavoriteList { UserId = user.Id, Name = "Ulubione" };
        db.FavoriteLists.Add(list);
        await db.SaveChangesAsync();
        db.FavoriteGames.Add(new FavoriteGame { FavoriteListId = list.Id, GameId = game.Id });
        await db.SaveChangesAsync();
        var service = new FavoriteService(db);

        var result = await service.RemoveFromFavoriteAsync(user.Id, game.Id, list.Id);

        Assert.True(result);
        Assert.Equal(0, await db.FavoriteGames.CountAsync());
    }

    [Fact]
    public async Task RemoveFromFavoriteAsync_NonExistingEntry_ReturnsFalse()
    {
        var db = CreateDb();
        var (user, game) = await SeedAsync(db);
        var service = new FavoriteService(db);

        var result = await service.RemoveFromFavoriteAsync(user.Id, game.Id, 999);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteListAsync_ExistingList_DeletesAndReturnsName()
    {
        var db = CreateDb();
        var (user, _) = await SeedAsync(db);
        var list = new FavoriteList { UserId = user.Id, Name = "Ulubione" };
        db.FavoriteLists.Add(list);
        await db.SaveChangesAsync();
        var service = new FavoriteService(db);

        var result = await service.DeleteListAsync(user.Id, list.Id);

        Assert.Equal("Ulubione", result);
        Assert.Equal(0, await db.FavoriteLists.CountAsync());
    }

    [Fact]
    public async Task DeleteListAsync_NonExistingList_ReturnsNull()
    {
        var db = CreateDb();
        var (user, _) = await SeedAsync(db);
        var service = new FavoriteService(db);

        var result = await service.DeleteListAsync(user.Id, 999);

        Assert.Null(result);
    }
}