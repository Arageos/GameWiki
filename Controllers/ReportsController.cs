using GameWiki.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    }
}