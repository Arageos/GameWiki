using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Services
{
    public class ReportService
    {
        private readonly GameWikiDbContext _context;
        private readonly NotificationService _notifications;

        public ReportService(GameWikiDbContext context, NotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        public async Task CreateReportAsync(int userId, string userName, ReportType type, int targetId, string reason)
        {
            _context.Reports.Add(new Report
            {
                ReporterId = userId,
                Type = type,
                TargetId = targetId,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            await _notifications.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe zgłoszenie ({type}) od {userName}: {reason.Substring(0, Math.Min(reason.Length, 60))}{(reason.Length > 60 ? "..." : "")}",
                "/Admin/Reports"
            );
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AppealBanAsync(string email, string message)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !user.IsBanned) return false;

            _context.Appeals.Add(new Appeal
            {
                UserId = user.Id,
                Subject = "Odwołanie od blokady konta",
                Message = message
            });

            await _notifications.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe odwołanie od zbanowanego użytkownika {user.Username}.",
                "/Admin/Appeals"
            );

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task CreateAppealAsync(int userId, string userName, string? subject, string message)
        {
            _context.Appeals.Add(new Appeal
            {
                UserId = userId,
                Subject = subject ?? "Odwołanie od decyzji administracji",
                Message = message
            });

            await _notifications.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe odwołanie od użytkownika {userName}.",
                "/Admin/Appeals"
            );

            await _context.SaveChangesAsync();
        }
    }
}