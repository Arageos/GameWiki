using GameWiki.Models;
using GameWiki.Services;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Tests;

public class NotificationServiceTests
{
    private GameWikiDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<GameWikiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GameWikiDbContext(options);
    }

    private async Task<User> SeedUserWithRoleAsync(GameWikiDbContext db, string roleName)
    {
        var role = new Role { Name = roleName };
        var user = new User { Username = roleName + "User", Email = roleName + "@test.com", PasswordHash = "hash" };
        db.Roles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public void NotifyUser_AddsNotificationToContext()
    {
        var db = CreateDb();
        var service = new NotificationService(db);

        service.NotifyUser(1, NotificationType.Ban, "Zostałeś zbanowany.");

        Assert.Equal(1, db.UserNotifications.Local.Count);
    }

    [Fact]
    public void NotifyUser_SetsCorrectFields()
    {
        var db = CreateDb();
        var service = new NotificationService(db);

        service.NotifyUser(5, NotificationType.ContentRemoved, "Usunięto treść.", reason: "Spam", actionUrl: "/home");

        var notification = db.UserNotifications.Local.First();
        Assert.Equal(5, notification.UserId);
        Assert.Equal(NotificationType.ContentRemoved, notification.Type);
        Assert.Equal("Spam", notification.Reason);
        Assert.Equal("/home", notification.ActionUrl);
    }

    [Fact]
    public async Task NotifyModsAsync_SendsNotificationToAllModsAndAdmins()
    {
        var db = CreateDb();
        var mod = await SeedUserWithRoleAsync(db, "Moderator");
        var admin = await SeedUserWithRoleAsync(db, "Admin");
        var user = await SeedUserWithRoleAsync(db, "User");
        var service = new NotificationService(db);

        await service.NotifyModsAsync(NotificationType.NewReport, "Nowe zgłoszenie");

        var notifications = db.UserNotifications.Local.ToList();
        Assert.Equal(2, notifications.Count);
        Assert.Contains(notifications, n => n.UserId == mod.Id);
        Assert.Contains(notifications, n => n.UserId == admin.Id);
        Assert.DoesNotContain(notifications, n => n.UserId == user.Id);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_ReturnsOnlyUserNotifications()
    {
        var db = CreateDb();
        var service = new NotificationService(db);
        service.NotifyUser(1, NotificationType.Ban, "Wiadomość dla 1");
        service.NotifyUser(2, NotificationType.Ban, "Wiadomość dla 2");
        await db.SaveChangesAsync();

        var result = await service.GetUserNotificationsAsync(1);

        Assert.Single(result);
        Assert.Equal(1, result[0].UserId);
    }

    [Fact]
    public async Task DeleteAsync_ExistingNotification_DeletesAndReturnsTrue()
    {
        var db = CreateDb();
        var service = new NotificationService(db);
        service.NotifyUser(1, NotificationType.Ban, "Test");
        await db.SaveChangesAsync();
        var notificationId = db.UserNotifications.First().Id;

        var result = await service.DeleteAsync(notificationId, 1);

        Assert.True(result);
        Assert.Equal(0, await db.UserNotifications.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_WrongUser_ReturnsFalse()
    {
        var db = CreateDb();
        var service = new NotificationService(db);
        service.NotifyUser(1, NotificationType.Ban, "Test");
        await db.SaveChangesAsync();
        var notificationId = db.UserNotifications.First().Id;

        var result = await service.DeleteAsync(notificationId, userId: 999);

        Assert.False(result);
        Assert.Equal(1, await db.UserNotifications.CountAsync());
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksAllUserNotificationsAsRead()
    {
        var db = CreateDb();
        var service = new NotificationService(db);
        service.NotifyUser(1, NotificationType.Ban, "Wiadomość 1");
        service.NotifyUser(1, NotificationType.Unban, "Wiadomość 2");
        await db.SaveChangesAsync();

        await service.MarkAllReadAsync(1);

        var notifications = await db.UserNotifications.Where(n => n.UserId == 1).ToListAsync();
        Assert.All(notifications, n => Assert.True(n.IsRead));
    }
}