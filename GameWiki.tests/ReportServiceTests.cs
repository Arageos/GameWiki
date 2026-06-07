using GameWiki.Models;
using GameWiki.Services;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Tests;

public class ReportServiceTests
{
    private GameWikiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameWikiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GameWikiDbContext(options);
    }

    private async Task<User> SeedUserAsync(GameWikiDbContext db, bool isBanned = false)
    {
        var user = new User { Username = "TestUser", Email = "test@test.com", PasswordHash = "hash", IsBanned = isBanned };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateReportAsync_SavesReportToDatabase()
    {
        var db = CreateDb();
        var notificationService = new NotificationService(db);
        var service = new ReportService(db, notificationService);

        await service.CreateReportAsync(1, "TestUser", ReportType.Comment, 5, "Wulgaryzmy");

        Assert.Equal(1, await db.Reports.CountAsync());
        var report = await db.Reports.FirstAsync();
        Assert.Equal(ReportType.Comment, report.Type);
        Assert.Equal(5, report.TargetId);
        Assert.Equal("Wulgaryzmy", report.Reason);
    }

    [Fact]
    public async Task CreateReportAsync_ReportHasPendingStatus()
    {
        var db = CreateDb();
        var notificationService = new NotificationService(db);
        var service = new ReportService(db, notificationService);

        await service.CreateReportAsync(1, "TestUser", ReportType.Article, 1, "Spam");

        var report = await db.Reports.FirstAsync();
        Assert.Equal(ReportStatus.Pending, report.Status);
    }

    [Fact]
    public async Task AppealBanAsync_BannedUser_CreatesAppealAndReturnsTrue()
    {
        var db = CreateDb();
        var user = await SeedUserAsync(db, isBanned: true);
        var notificationService = new NotificationService(db);
        var service = new ReportService(db, notificationService);

        var result = await service.AppealBanAsync(user.Email, "Proszę odbanować mnie.");

        Assert.True(result);
        Assert.Equal(1, await db.Appeals.CountAsync());
    }

    [Fact]
    public async Task AppealBanAsync_NotBannedUser_ReturnsFalse()
    {
        var db = CreateDb();
        var user = await SeedUserAsync(db, isBanned: false);
        var notificationService = new NotificationService(db);
        var service = new ReportService(db, notificationService);

        var result = await service.AppealBanAsync(user.Email, "Proszę odbanować.");

        Assert.False(result);
        Assert.Equal(0, await db.Appeals.CountAsync());
    }

    [Fact]
    public async Task AppealBanAsync_NonExistingEmail_ReturnsFalse()
    {
        var db = CreateDb();
        var notificationService = new NotificationService(db);
        var service = new ReportService(db, notificationService);

        var result = await service.AppealBanAsync("nieistnieje@test.com", "Odwołanie");

        Assert.False(result);
    }

    [Fact]
    public async Task AppealBanAsync_SetsCorrectSubject()
    {
        var db = CreateDb();
        var user = await SeedUserAsync(db, isBanned: true);
        var notificationService = new NotificationService(db);
        var service = new ReportService(db, notificationService);

        await service.AppealBanAsync(user.Email, "Proszę odbanować.");

        var appeal = await db.Appeals.FirstAsync();
        Assert.Equal("Odwołanie od blokady konta", appeal.Subject);
    }

    [Fact]
    public async Task CreateAppealAsync_SavesAppealToDatabase()
    {
        var db = CreateDb();
        var notificationService = new NotificationService(db);
        var service = new ReportService(db, notificationService);

        await service.CreateAppealAsync(1, "TestUser", "Mój temat", "Treść odwołania");

        Assert.Equal(1, await db.Appeals.CountAsync());
        var appeal = await db.Appeals.FirstAsync();
        Assert.Equal("Mój temat", appeal.Subject);
        Assert.Equal("Treść odwołania", appeal.Message);
    }

    [Fact]
    public async Task CreateAppealAsync_NullSubject_UsesDefaultSubject()
    {
        var db = CreateDb();
        var notificationService = new NotificationService(db);
        var service = new ReportService(db, notificationService);

        await service.CreateAppealAsync(1, "TestUser", null, "Treść odwołania");

        var appeal = await db.Appeals.FirstAsync();
        Assert.Equal("Odwołanie od decyzji administracji", appeal.Subject);
    }
}