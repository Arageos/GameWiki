using GameWiki.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    [Authorize] // Tylko zalogowani mogą zgłaszać
    public class ReportsController : Controller
    {
        private readonly GameWikiDbContext _context;

        public ReportsController(GameWikiDbContext context)
        {
            _context = context;
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

            // 1. Zapisujemy zgłoszenie
            var report = new Report
            {
                ReporterId = userId,
                Type = type,
                TargetId = targetId,
                Reason = reason
            };
            _context.Reports.Add(report);

            // 2. Tworzymy powiadomienie "Dzwoneczek" dla administracji
            var notification = new ModNotification
            {
                Message = $"Nowe zgłoszenie ({type}) od użytkownika {User.Identity?.Name}.",
                ActionUrl = "/Admin/Reports" // Ten widok stworzymy w Etapie 4
            };
            _context.ModNotifications.Add(notification);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Zgłoszenie zostało wysłane do administracji. Dziękujemy za reakcję!";
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

            var notification = new ModNotification
            {
                Message = $"Nowe odwołanie od zbanowanego użytkownika {user.Username}.",
                ActionUrl = "/Admin/Appeals"
            };
            _context.ModNotifications.Add(notification);

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

            // Dzwoneczek dla administracji
            var notification = new ModNotification
            {
                Message = $"Nowe odwołanie od użytkownika {User.Identity?.Name}.",
                ActionUrl = "/Admin/Appeals" // Tym panelem zajmiemy się w kolejnym etapie
            };
            _context.ModNotifications.Add(notification);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Twoje odwołanie zostało wysłane. Administracja wkrótce się nim zajmie.";
            return Redirect(returnUrl ?? "/Account/Notifications");
        }
    }
}