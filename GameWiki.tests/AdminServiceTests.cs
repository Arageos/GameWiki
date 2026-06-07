using GameWiki.Models;
using GameWiki.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GameWiki.Tests;

public class AdminServiceTests
{
    private GameWikiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameWikiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GameWikiDbContext(options);
    }

    private AdminService CreateService(GameWikiDbContext db)
    {
        var mockNotifications = new Mock<NotificationService>(db);
        var mockReviews = new Mock<ReviewService>(db, mockNotifications.Object);
        return new AdminService(db, mockNotifications.Object, mockReviews.Object);
    }

    private async Task<User> SeedUserWithRoleAsync(GameWikiDbContext db, string roleName)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName)
                   ?? new Role { Name = roleName };
        if (role.Id == 0) { db.Roles.Add(role); await db.SaveChangesAsync(); }

        var user = new User { Username = roleName + "User", Email = roleName + "@test.com", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsAllUsers()
    {
        var db = CreateDb();
        await SeedUserWithRoleAsync(db, "User");
        await SeedUserWithRoleAsync(db, "Moderator");
        var service = CreateService(db);

        var result = await service.GetUsersAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ToggleBanAsync_CannotBanSelf_ReturnsFalse()
    {
        var db = CreateDb();
        var user = await SeedUserWithRoleAsync(db, "User");
        var service = CreateService(db);

        var (success, message, _) = await service.ToggleBanAsync(user.Id, user.Id, "Admin", null);

        Assert.False(success);
        Assert.Contains("siebie", message);
    }

    [Fact]
    public async Task ToggleBanAsync_ModeratorCannotBanModerator_ReturnsFalse()
    {
        var db = CreateDb();
        var mod1 = await SeedUserWithRoleAsync(db, "Moderator");
        var mod2 = await SeedUserWithRoleAsync(db, "Moderator");
        var service = CreateService(db);

        var (success, message, _) = await service.ToggleBanAsync(mod2.Id, mod1.Id, "Moderator", null);

        Assert.False(success);
    }

    [Fact]
    public async Task ToggleBanAsync_ValidBan_BansUserAndReturnsTrue()
    {
        var db = CreateDb();
        var admin = await SeedUserWithRoleAsync(db, "Admin");
        var user = await SeedUserWithRoleAsync(db, "User");
        var service = CreateService(db);

        var (success, _, _) = await service.ToggleBanAsync(user.Id, admin.Id, "Admin", "Spam");

        Assert.True(success);
        var bannedUser = await db.Users.FindAsync(user.Id);
        Assert.True(bannedUser!.IsBanned);
    }

    [Fact]
    public async Task ToggleBanAsync_UnbanUser_SetsIsBannedFalse()
    {
        var db = CreateDb();
        var admin = await SeedUserWithRoleAsync(db, "Admin");
        var user = await SeedUserWithRoleAsync(db, "User");
        user.IsBanned = true;
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.ToggleBanAsync(user.Id, admin.Id, "Admin", null);

        var unbannedUser = await db.Users.FindAsync(user.Id);
        Assert.False(unbannedUser!.IsBanned);
    }

    [Fact]
    public async Task ToggleModRoleAsync_CannotChangeAdmin_ReturnsFalse()
    {
        var db = CreateDb();
        var admin = await SeedUserWithRoleAsync(db, "Admin");
        var service = CreateService(db);

        var (success, message) = await service.ToggleModRoleAsync(admin.Id);

        Assert.False(success);
        Assert.Contains("Administrator", message);
    }

    [Fact]
    public async Task ToggleModRoleAsync_PromoteUserToMod_ReturnsTrue()
    {
        var db = CreateDb();
        var user = await SeedUserWithRoleAsync(db, "User");
        var modRole = new Role { Name = "Moderator" };
        db.Roles.Add(modRole);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var (success, message) = await service.ToggleModRoleAsync(user.Id);

        Assert.True(success);
        Assert.Contains("Moderatora", message);
    }

    [Fact]
    public async Task GetPendingArticlesAsync_ReturnsOnlyUnverified()
    {
        var db = CreateDb();
        var user = await SeedUserWithRoleAsync(db, "User");
        var game = new Game { Title = "Gra", Description = "Opis", ReleaseDate = DateTime.UtcNow };
        db.Games.Add(game);
        await db.SaveChangesAsync();
        db.Articles.AddRange(
            new Article { Title = "Oczekujący", GameId = game.Id, AuthorId = user.Id, IsVerified = false, CreatedAt = DateTime.UtcNow },
            new Article { Title = "Zatwierdzony", GameId = game.Id, AuthorId = user.Id, IsVerified = true, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetPendingArticlesAsync();

        Assert.Single(result);
        Assert.Equal("Oczekujący", result[0].Title);
    }

    [Fact]
    public async Task HandleReportAsync_NonExistingReport_ReturnsFalse()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var (found, _) = await service.HandleReportAsync(999, "dismiss", null);

        Assert.False(found);
    }

    [Fact]
    public async Task HandleReportAsync_DismissReport_SetsStatusResolved()
    {
        var db = CreateDb();
        var user = await SeedUserWithRoleAsync(db, "User");
        var report = new Report { ReporterId = user.Id, Type = ReportType.Article, TargetId = 1, Reason = "Spam", CreatedAt = DateTime.UtcNow };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var (found, _) = await service.HandleReportAsync(report.Id, "dismiss", null);

        Assert.True(found);
        var updated = await db.Reports.FindAsync(report.Id);
        Assert.Equal(ReportStatus.Resolved, updated!.Status);
    }

    [Fact]
    public async Task GetAppealsAsync_ReturnsOnlyPending()
    {
        var db = CreateDb();
        var user = await SeedUserWithRoleAsync(db, "User");
        db.Appeals.AddRange(
            new Appeal { UserId = user.Id, Subject = "Test", Message = "Msg", Status = AppealStatus.Pending },
            new Appeal { UserId = user.Id, Subject = "Test2", Message = "Msg2", Status = AppealStatus.Approved }
        );
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetAppealsAsync();

        Assert.Single(result);
        Assert.Equal(AppealStatus.Pending, result[0].Status);
    }

    [Fact]
    public async Task HandleAppealAsync_Approve_UnbansUserAndSetsStatusApproved()
    {
        var db = CreateDb();
        var user = await SeedUserWithRoleAsync(db, "User");
        user.IsBanned = true;
        var appeal = new Appeal { UserId = user.Id, Subject = "Odwołanie", Message = "Proszę odbanować", Status = AppealStatus.Pending };
        db.Appeals.Add(appeal);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var (found, _) = await service.HandleAppealAsync(appeal.Id, "approve");

        Assert.True(found);
        var updatedUser = await db.Users.FindAsync(user.Id);
        Assert.False(updatedUser!.IsBanned);
        var updatedAppeal = await db.Appeals.FindAsync(appeal.Id);
        Assert.Equal(AppealStatus.Approved, updatedAppeal!.Status);
    }

    [Fact]
    public async Task HandleAppealAsync_Reject_SetsStatusRejected()
    {
        var db = CreateDb();
        var user = await SeedUserWithRoleAsync(db, "User");
        var appeal = new Appeal { UserId = user.Id, Subject = "Odwołanie", Message = "Proszę odbanować", Status = AppealStatus.Pending };
        db.Appeals.Add(appeal);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.HandleAppealAsync(appeal.Id, "reject");

        var updatedAppeal = await db.Appeals.FindAsync(appeal.Id);
        Assert.Equal(AppealStatus.Rejected, updatedAppeal!.Status);
    }
}