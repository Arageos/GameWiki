using GameWiki.DTOs.Review;
using GameWiki.Models;
using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly GameWikiDbContext _context;
        private readonly ArticleService _articleService;

        public ReviewsController(GameWikiDbContext context, ArticleService articleService)
        {
            _context = context;
            _articleService = articleService;
        }

        public async Task<IActionResult> Index(int gameId)
        {
            var game = await _context.Games.FindAsync(gameId);
            if (game == null) return NotFound();

            var userId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
                : (int?)null;

            var reviews = await _context.Reviews
                .Where(r => r.GameId == gameId)
                .Include(r => r.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto
                {
                    Id = r.Id,
                    GameId = r.GameId,
                    UserId = r.UserId,
                    GameTitle = game.Title,
                    Username = r.User.Username,
                    AvatarUrl = r.User.ProfilePictureUrl,
                    AuthorRoleName = r.User.UserRoles
                        .Select(ur => ur.Role.Name)
                        .FirstOrDefault(),
                    Rating = r.Rating,
                    Content = r.Content,
                    CreatedAt = r.CreatedAt,
                    IsOwner = userId != null && r.UserId == userId,
                    IsVerified = r.IsVerified
                })
                .ToListAsync();

            ViewBag.GameId = gameId;
            ViewBag.GameTitle = game.Title;
            ViewBag.BackgroundImage = game.BackgroundImage;

            return View(reviews);
        }

        [Authorize]
        public async Task<IActionResult> Create(int gameId)
        {
            var game = await _context.Games.FindAsync(gameId);
            if (game == null) return NotFound();

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var existing = await _context.Reviews
                .FirstOrDefaultAsync(r => r.GameId == gameId && r.UserId == userId);

            if (existing != null)
                return RedirectToAction(nameof(Edit), new { id = existing.Id });

            var dto = new CreateReviewDto { GameId = gameId };
            ViewBag.GameTitle = game.Title;
            ViewBag.BackgroundImage = game.BackgroundImage;
            return View(dto);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateReviewDto dto)
        {
            if (!ModelState.IsValid)
            {
                var g = await _context.Games.FindAsync(dto.GameId);
                ViewBag.GameTitle = g?.Title;
                ViewBag.BackgroundImage = g?.BackgroundImage;
                return View(dto);
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var exists = await _context.Reviews
                .AnyAsync(r => r.GameId == dto.GameId && r.UserId == userId);

            if (exists)
            {
                TempData["Error"] = "Już wystawiłeś recenzję tej gry.";
                return RedirectToAction("Details", "Games", new { id = dto.GameId });
            }

            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");
            bool hasContent = !string.IsNullOrWhiteSpace(dto.Content);

            bool isVerified = isMod || !hasContent;

            var review = new Review
            {
                GameId = dto.GameId,
                UserId = userId,
                Rating = dto.Rating,
                Content = dto.Content,
                IsVerified = isVerified,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            if (!isVerified && hasContent)
            {
                var author = await _context.Users.FindAsync(userId);
                var game = await _context.Games.FindAsync(dto.GameId);
                await _articleService.NotifyModsAsync(
                    NotificationType.NewReview,
                    $"Nowa recenzja do weryfikacji — gra: {game?.Title}, autor: { author?.Username}",
                    "/Admin/PendingReviews"
                );
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = isVerified
                ? "Recenzja została opublikowana."
                : "Recenzja została dodana i oczekuje na weryfikację moderatora.";

            return RedirectToAction(nameof(Index), new { gameId = dto.GameId });
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");

            var review = await _context.Reviews
                .Include(r => r.Game)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id && (r.UserId == userId || isMod));

            if (review == null) return NotFound();

            var dto = new EditReviewDto
            {
                Id = review.Id,
                GameId = review.GameId,
                GameTitle = review.Game.Title,
                Rating = review.Rating,
                Content = review.Content
            };

            ViewBag.BackgroundImage = review.Game.BackgroundImage;
            ViewBag.IsModEdit = isMod && review.UserId != userId;
            ViewBag.OriginalAuthor = review.User?.Username;
            return View(dto);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditReviewDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");

            var review = await _context.Reviews
                .Include(r => r.Game)
                .FirstOrDefaultAsync(r => r.Id == id && (r.UserId == userId || isMod));

            if (review == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.BackgroundImage = review.Game?.BackgroundImage;
                return View(dto);
            }

            bool isModEdit = isMod && review.UserId != userId;
            review.Rating = dto.Rating;
            review.Content = dto.Content;

            if (isModEdit)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = review.UserId,
                    Type = NotificationType.ContentEdited,
                    Message = $"Twoja recenzja gry {review.Game?.Title} została zedytowana przez moderację.",
                    ActionUrl = $"/Reviews/Index?gameId={review.GameId}",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = isModEdit
                ? "Recenzja użytkownika została zaktualizowana."
                : "Twoja recenzja została zaktualizowana.";

            return RedirectToAction(nameof(Index), new { gameId = review.GameId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (review == null) return NotFound();

            await RemoveReviewAsync(review);

            TempData["SuccessMessage"] = "Recenzja została usunięta.";
            return RedirectToAction(nameof(Index), new { gameId = review.GameId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteByMod(int id, string? deleteReason, string? returnUrl)
        {
            var review = await _context.Reviews
                .Include(r => r.Game)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (review == null) return NotFound();

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            _context.UserNotifications.Add(new UserNotification
            {
                UserId = review.UserId,
                Type = NotificationType.ContentRemoved,
                Message = $"Twoja recenzja gry {review.Game?.Title} została usunięta przez moderację.",
                Reason = string.IsNullOrWhiteSpace(deleteReason) ? "Naruszenie regulaminu." : deleteReason,
                CreatedAt = DateTime.UtcNow
            });

            await RemoveReviewAsync(review);

            TempData["SuccessMessage"] = "Recenzja została usunięta i autor został powiadomiony.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index), new { gameId = review.GameId });
        }

        private async Task RemoveReviewAsync(Review review)
        {
            var relatedReports = await _context.Reports
                .Where(r => r.Type == ReportType.Review && r.TargetId == review.Id && r.Status == ReportStatus.Pending)
                .ToListAsync();
            foreach (var r in relatedReports)
                r.Status = ReportStatus.Resolved;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
        }
    }
}