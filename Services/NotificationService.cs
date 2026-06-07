using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Services
{
    public class NotificationService
    {
        private readonly GameWikiDbContext _context;

        public NotificationService(GameWikiDbContext context)
        {
            _context = context;
        }

        public async Task NotifyModsAsync(NotificationType type, string message, string? actionUrl = null)
        {
            var modAdminIds = await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.Role.Name == "Admin" || ur.Role.Name == "Moderator")
                .Select(ur => ur.UserId)
                .ToListAsync();

            foreach (var uid in modAdminIds)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId    = uid,
                    Type      = type,
                    Message   = message,
                    ActionUrl = actionUrl,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        public void NotifyUser(int userId, NotificationType type, string message,
                               string? reason = null, string? actionUrl = null)
        {
            _context.UserNotifications.Add(new UserNotification
            {
                UserId    = userId,
                Type      = type,
                Message   = message,
                Reason    = reason,
                ActionUrl = actionUrl,
                CreatedAt = DateTime.UtcNow
            });
        }

        public async Task<List<UserNotification>> GetUserNotificationsAsync(int userId)
        {
            return await _context.UserNotifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteAsync(int notificationId, int userId)
        {
            var notification = await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null) return false;

            _context.UserNotifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task MarkAllReadAsync(int userId)
        {
            var unread = await _context.UserNotifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _context.SaveChangesAsync();
        }
    }
}
