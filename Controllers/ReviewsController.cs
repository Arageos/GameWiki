using GameWiki.DTOs.Review;
using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly GameWikiDbContext _context;
        private readonly ReviewService _reviews;

        public ReviewsController(GameWikiDbContext context, ReviewService reviews)
        {
            _context = context;
            _reviews = reviews;
        }

        public async Task<IActionResult> Index(int gameId)
        {
            var game = await _context.Games.FindAsync(gameId);
            if (game == null) return NotFound();

            var reviews = await _reviews.GetReviewsAsync(gameId, GetUserIdOrNull());

            ViewBag.GameId          = gameId;
            ViewBag.GameTitle       = game.Title;
            ViewBag.BackgroundImage = game.BackgroundImage;

            return View(reviews);
        }

        [Authorize]
        public async Task<IActionResult> Create(int gameId)
        {
            var game = await _context.Games.FindAsync(gameId);
            if (game == null) return NotFound();

            var existing = await _reviews.GetExistingReviewAsync(gameId, GetUserId());
            if (existing != null)
                return RedirectToAction(nameof(Edit), new { id = existing.Id });

            ViewBag.GameTitle       = game.Title;
            ViewBag.BackgroundImage = game.BackgroundImage;
            return View(new CreateReviewDto { GameId = gameId });
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateReviewDto dto)
        {
            if (!ModelState.IsValid)
            {
                var g = await _context.Games.FindAsync(dto.GameId);
                ViewBag.GameTitle = g?.Title; ViewBag.BackgroundImage = g?.BackgroundImage;
                return View(dto);
            }

            var userId = GetUserId();
            if (await _reviews.GetExistingReviewAsync(dto.GameId, userId) != null)
            {
                TempData["Error"] = "Już wystawiłeś recenzję tej gry.";
                return RedirectToAction("Details", "Games", new { id = dto.GameId });
            }

            bool isMod = await IsModAsync(userId);
            var review = await _reviews.CreateAsync(dto.GameId, userId, dto, isMod);

            TempData["SuccessMessage"] = review.IsVerified
                ? "Recenzja została opublikowana."
                : "Recenzja została dodana i oczekuje na weryfikację moderatora.";

            return RedirectToAction(nameof(Index), new { gameId = dto.GameId });
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetUserId();
            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");
            var review = await _reviews.GetReviewForEditAsync(id, userId, isMod);
            if (review == null) return NotFound();

            ViewBag.BackgroundImage = review.Game?.BackgroundImage;
            ViewBag.IsModEdit       = isMod && review.UserId != userId;
            ViewBag.OriginalAuthor  = review.User?.Username;

            return View(new EditReviewDto
            {
                Id = review.Id, GameId = review.GameId,
                GameTitle = review.Game!.Title, Rating = review.Rating, Content = review.Content
            });
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditReviewDto dto)
        {
            var userId = GetUserId();
            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");
            var review = await _reviews.GetReviewForEditAsync(id, userId, isMod);
            if (review == null) return NotFound();

            if (!ModelState.IsValid) { ViewBag.BackgroundImage = review.Game?.BackgroundImage; return View(dto); }

            bool isModEdit = isMod && review.UserId != userId;
            await _reviews.UpdateAsync(review, dto, userId);

            TempData["SuccessMessage"] = isModEdit
                ? "Recenzja użytkownika została zaktualizowana."
                : "Twoja recenzja została zaktualizowana.";

            return RedirectToAction(nameof(Index), new { gameId = review.GameId });
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _reviews.GetReviewForEditAsync(id, GetUserId(), isMod: false);
            if (review == null) return NotFound();

            await _reviews.DeleteAsync(review);
            TempData["SuccessMessage"] = "Recenzja została usunięta.";
            return RedirectToAction(nameof(Index), new { gameId = review.GameId });
        }

        [HttpPost, Authorize(Roles = "Admin,Moderator"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteByMod(int id, string? deleteReason, string? returnUrl)
        {
            var review = await _reviews.GetReviewForEditAsync(id, 0, isMod: true);
            if (review == null) return NotFound();

            await _reviews.DeleteByModAsync(review, deleteReason);
            TempData["SuccessMessage"] = "Recenzja została usunięta i autor został powiadomiony.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index), new { gameId = review.GameId });
        }

        private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        private int? GetUserIdOrNull()
        {
            var v = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(v, out var id) ? id : null;
        }
        private async Task<bool> IsModAsync(int userId)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Moderator")) return true;
            var role = await _reviews.GetUserRoleAsync(userId);
            return role == "Admin" || role == "Moderator";
        }
    }
}
