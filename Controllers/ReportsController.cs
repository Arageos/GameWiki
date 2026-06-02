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
        private readonly ArticleService _articleService;

        public ReportsController(GameWikiDbContext context, ArticleService articleService)
        {
            _context = context;
            _articleService = articleService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(ReportType type, int targetId, string reason, string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] = "Musisz podać powód zgłoszenia.";
                return Redirect(returnUrl ?? "/");
            }

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return RedirectToAction("Login", "Account");

            var report = new Report
            {
                ReporterId = userId,
                Type = type,
                TargetId = targetId,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };
            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            // Powiadom wszystkich Admin/Mod przez UserNotification (widoczne w dzwonku)
            await _articleService.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe zgłoszenie ({type}) od użytkownika {User.Identity?.Name}: {reason.Substring(0, Math.Min(reason.Length, 60))}{(reason.Length > 60 ? "…" : "")}",
                "/Admin/Reports"
            );
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Zgłoszenie zostało wysłane do administracji. Dziękujemy!";
            return Redirect(returnUrl ?? "/");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> AppealBan(string email, string message)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !user.IsBanned) return RedirectToAction("Login", "Account");

            var appeal = new Appeal
            {
                UserId = user.Id,
                Subject = "Odwołanie od blokady konta",
                Message = message
            };
            _context.Appeals.Add(appeal);

            // Powiadom moderatorów
            await _articleService.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe odwołanie od zbanowanego użytkownika {user.Username}.",
                "/Admin/Appeals"
            );

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Twoje odwołanie zostało wysłane. Administracja wkrótce je rozpatrzy.";
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

            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return RedirectToAction("Login", "Account");

            var appeal = new Appeal
            {
                UserId = userId,
                Subject = subject ?? "Odwołanie od decyzji administracji",
                Message = message
            };
            _context.Appeals.Add(appeal);

            await _articleService.NotifyModsAsync(
                NotificationType.NewReport,
                $"Nowe odwołanie od użytkownika {User.Identity?.Name}.",
                "/Admin/Appeals"
            );

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Twoje odwołanie zostało wysłane. Administracja wkrótce się nim zajmie.";
            return Redirect(returnUrl ?? "/Account/Notifications");
        }
    }
}