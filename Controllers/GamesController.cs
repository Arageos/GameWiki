using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameWiki.Models;
using GameWiki.DTOs.Game;

namespace GameWiki.Controllers
{
    public class GamesController : Controller
    {
        private readonly GameWikiDbContext _context;

        public GamesController(GameWikiDbContext context)
        {
            _context = context;
        }

        // GET: Games
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

        // GET: Games/Details/5
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

        // GET: Games/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Games/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
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

        // GET: Games/Edit/5
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

        // POST: Games/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
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

        // GET: Games/Delete/5
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

        // POST: Games/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
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