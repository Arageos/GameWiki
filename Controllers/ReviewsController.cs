using GameWiki.DTOs.Review;
using GameWiki.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly GameWikiDbContext _context;

        public ReviewsController(GameWikiDbContext context)
        {
            _context = context;
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
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto
                {
                    Id = r.Id,
                    GameId = r.GameId,
                    GameTitle = game.Title,
                    Username = r.User.Username,
                    AvatarUrl = r.User.ProfilePictureUrl,
                    Rating = r.Rating,
                    Content = r.Content,
                    CreatedAt = r.CreatedAt,
                    IsOwner = userId != null && r.UserId == userId
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
                var game = await _context.Games.FindAsync(dto.GameId);
                ViewBag.GameTitle = game?.Title;
                ViewBag.BackgroundImage = game?.BackgroundImage;
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

            var review = new Review
            {
                GameId = dto.GameId,
                UserId = userId,
                Rating = dto.Rating,
                Content = dto.Content,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { gameId = dto.GameId });
        }

        
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var review = await _context.Reviews
                .Include(r => r.Game)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

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
            return View(dto);
        }

        
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditReviewDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (review == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.BackgroundImage = (await _context.Games.FindAsync(dto.GameId))?.BackgroundImage;
                return View(dto);
            }

            review.Rating = dto.Rating;
            review.Content = dto.Content;
            await _context.SaveChangesAsync();

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

            var gameId = review.GameId;
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { gameId });
        }
    }
}