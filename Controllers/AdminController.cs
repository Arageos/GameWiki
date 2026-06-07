using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminController : Controller
    {
        private readonly AdminService _admin;

        public AdminController(AdminService admin)
        {
            _admin = admin;
        }

        public async Task<IActionResult> Index()
            => View(await _admin.GetUsersAsync());

        [HttpPost]
        public async Task<IActionResult> ToggleBan(int userId, string? banReason)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

            var (success, message, _) = await _admin.ToggleBanAsync(userId, currentUserId, currentUserRole, banReason);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleModRole(int userId)
        {
            var (success, message) = await _admin.ToggleModRoleAsync(userId);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> PendingArticles()
            => View(await _admin.GetPendingArticlesAsync());

        public async Task<IActionResult> PendingReviews()
            => View(await _admin.GetPendingReviewsAsync());

        [HttpPost]
        public async Task<IActionResult> VerifyReview(int reviewId)
        {
            await _admin.VerifyReviewAsync(reviewId);
            TempData["SuccessMessage"] = "Recenzja została zweryfikowana.";
            return RedirectToAction(nameof(PendingReviews));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteReview(int reviewId, string? deleteReason)
        {
            var review = await _admin.GetReviewWithGameAsync(reviewId);
            if (review == null) return NotFound();

            await _admin.DeleteReviewByModAsync(review, deleteReason);
            TempData["SuccessMessage"] = "Recenzja została odrzucona i autor powiadomiony.";
            return RedirectToAction(nameof(PendingReviews));
        }

        public async Task<IActionResult> Reports()
            => View(await _admin.GetReportsAsync());

        [HttpPost]
        public async Task<IActionResult> HandleReport(int reportId, string actionType, string? deleteReason)
        {
            var (found, message) = await _admin.HandleReportAsync(reportId, actionType, deleteReason);
            if (!found) return NotFound();

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Reports));
        }

        public async Task<IActionResult> Appeals()
            => View(await _admin.GetAppealsAsync());

        [HttpPost]
        public async Task<IActionResult> HandleAppeal(int appealId, string actionType)
        {
            var (found, message) = await _admin.HandleAppealAsync(appealId, actionType);
            if (!found) return NotFound();

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Appeals));
        }
    }
}