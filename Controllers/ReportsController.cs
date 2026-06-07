using GameWiki.Models;
using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly GameWikiDbContext _context;
        private readonly NotificationService _notifications;

        public ReportsController(GameWikiDbContext context, NotificationService notifications)
        {
            _context       = context;
            _notifications = notifications;
        }

        [HttpPost]
        public async Task<IActionResult> Create(ReportType type, int targetId, string reason, string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] = "Musisz podać powód zgłoszenia.";
                return Redirect(returnUrl ?? "/");
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            _context.Reports.Add(new Report
            {
                ReporterId = userId,
                Type       = type,
                TargetId   = targetId,
                Reason     = reason,
                CreatedAt  = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            await _notifications.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe zgłoszenie ({type}) od {User.Identity?.Name}: {reason.Substring(0, Math.Min(reason.Length, 60))}{(reason.Length > 60 ? "…" : "")}",
                "/Admin/Reports"
            );
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Zgłoszenie wysłane do administracji. Dziękujemy!";
            return Redirect(returnUrl ?? "/");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> AppealBan(string email, string message)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !user.IsBanned) return RedirectToAction("Login", "Account");

            _context.Appeals.Add(new Appeal
            {
                UserId  = user.Id,
                Subject = "Odwołanie od blokady konta",
                Message = message
            });

            await _notifications.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe odwołanie od zbanowanego użytkownika {user.Username}.",
                "/Admin/Appeals"
            );

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Odwołanie wysłane. Administracja wkrótce je rozpatrzy.";
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppeal(string subject, string message, string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Musisz podać treść odwołania.";
                return Redirect(returnUrl ?? "/Account/Notifications");
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            _context.Appeals.Add(new Appeal
            {
                UserId  = userId,
                Subject = subject ?? "Odwołanie od decyzji administracji",
                Message = message
            });

            await _notifications.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe odwołanie od użytkownika {User.Identity?.Name}.",
                "/Admin/Appeals"
            );

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Odwołanie wysłane. Administracja wkrótce się nim zajmie.";
            return Redirect(returnUrl ?? "/Account/Notifications");
        }
    }
}
