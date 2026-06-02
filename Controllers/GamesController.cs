using GameWiki.DTOs.Game;
using GameWiki.Models;
using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Controllers
{
    public class GamesController : Controller
    {
        private readonly GameWikiDbContext _context;
        private readonly GameService _gameService;
        private readonly ArticleService _articleService;

        public GamesController(GameWikiDbContext context, GameService gameService, ArticleService articleService)
        {
            _context = context;
            _gameService = gameService;
            _articleService = articleService;
        }

        public async Task<IActionResult> Index(string? search, int? genreId, int? platformId)
        {
            var vm = await _gameService.GetGameIndexViewModelAsync(search, genreId, platformId);
            return View(vm);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var game = await _context.Games
                .Include(g => g.Reviews)
                .Where(g => g.Id == id)
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    Title = g.Title,
                    Description = g.Description,
                    ReleaseDate = g.ReleaseDate,
                    BackgroundImage = g.BackgroundImage,
                    RawgRating = g.RawgRating,
                    RawgRatingsCount = g.RawgRatingsCount,
                    LocalRating = g.Reviews.Any()
                        ? Math.Round(g.Reviews.Average(r => r.Rating), 1)
                        : null,
                    LocalRatingsCount = g.Reviews.Count()
                })
                .FirstOrDefaultAsync();

            if (game == null) return NotFound();
            return View(game);
        }

        [Authorize(Roles = "Admin,Moderator")]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> Create(CreateGameDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var game = new Game
            {
                Title = dto.Title,
                Description = dto.Description,
                ReleaseDate = dto.ReleaseDate
            };

            _context.Add(game);
            await _context.SaveChangesAsync();

            await _articleService.NotifyModsAsync(
                NotificationType.NewGame,
                $"Nowa gra dodana do bazy: {game.Title}.",
                $"/Games/Details/{game.Id}"
            );
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var game = await _context.Games.FindAsync(id);
            if (game == null) return NotFound();

            return View(new UpdateGameDto
            {
                Id = game.Id,
                Title = game.Title,
                Description = game.Description,
                ReleaseDate = game.ReleaseDate
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> Edit(int id, UpdateGameDto dto)
        {
            if (id != dto.Id) return NotFound();
            if (!ModelState.IsValid) return View(dto);

            var game = await _context.Games.FindAsync(id);
            if (game == null) return NotFound();

            game.Title = dto.Title;
            game.Description = dto.Description;
            game.ReleaseDate = dto.ReleaseDate;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var game = await _context.Games
                .Where(g => g.Id == id)
                .Select(g => new GameDto { Id = g.Id, Title = g.Title, Description = g.Description, ReleaseDate = g.ReleaseDate })
                .FirstOrDefaultAsync();
            if (game == null) return NotFound();
            return View(game);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var game = await _context.Games.FindAsync(id);
            if (game != null)
            {
                _context.Games.Remove(game);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> SearchTitles(string term)
        {
            var titles = await _gameService.GetGameTitlesAsync(term);
            return Json(titles);
        }
    }
}