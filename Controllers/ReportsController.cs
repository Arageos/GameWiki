using GameWiki.Models;
using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ReportService _reports;

        public ReportsController(ReportService reports)
        {
            _reports = reports;
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
            await _reports.CreateReportAsync(userId, User.Identity?.Name ?? "", type, targetId, reason);

            TempData["SuccessMessage"] = "Zgłoszenie wysłane do administracji. Dziękujemy!";
            return Redirect(returnUrl ?? "/");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> AppealBan(string email, string message)
        {
            var success = await _reports.AppealBanAsync(email, message);
            if (!success) return RedirectToAction("Login", "Account");

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
            await _reports.CreateAppealAsync(userId, User.Identity?.Name ?? "", subject, message);

            TempData["SuccessMessage"] = "Odwołanie wysłane. Administracja wkrótce się nim zajmie.";
            return Redirect(returnUrl ?? "/Account/Notifications");
        }
    }
}