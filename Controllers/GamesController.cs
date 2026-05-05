using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameWiki.Models;
using GameWiki.DTOs.Game;
using Microsoft.AspNetCore.Authorization;

namespace GameWiki.Controllers
{
    public class GamesController : Controller
    {
        private readonly GameWikiDbContext _context;

        public GamesController(GameWikiDbContext context)
        {
            _context = context;
        }

        
        public async Task<IActionResult> Index()
        {
            var games = await _context.Games
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    Title = g.Title,
                    Description = g.Description,
                    ReleaseDate = g.ReleaseDate
                })
                .ToListAsync();

            return View(games);
        }

        
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var game = await _context.Games
                .Where(g => g.Id == id)
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    Title = g.Title,
                    Description = g.Description,
                    ReleaseDate = g.ReleaseDate
                })
                .FirstOrDefaultAsync();

            if (game == null) return NotFound();

            return View(game);
        }

        [Authorize(Roles = "Admin,Moderator")]
        public IActionResult Create()
        {
            return View();
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> Create(CreateGameDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var game = new Game
            {
                Title = dto.Title,
                Description = dto.Description,
                ReleaseDate = dto.ReleaseDate
            };

            _context.Add(game);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var game = await _context.Games.FindAsync(id);

            if (game == null) return NotFound();

            var dto = new UpdateGameDto
            {
                Id = game.Id,
                Title = game.Title,
                Description = game.Description,
                ReleaseDate = game.ReleaseDate
            };

            return View(dto);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> Edit(int id, UpdateGameDto dto)
        {
            if (id != dto.Id) return NotFound();

            if (!ModelState.IsValid)
                return View(dto);

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
                .Select(g => new GameDto
                {
                    Id = g.Id,
                    Title = g.Title,
                    Description = g.Description,
                    ReleaseDate = g.ReleaseDate
                })
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
    }
}