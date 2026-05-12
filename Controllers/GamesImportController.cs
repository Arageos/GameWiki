using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameWiki.Models;
using GameWiki.Services;

namespace GameWiki.Controllers
{
    [Authorize(Roles = "Admin,Moderator")]
    public class GamesImportController : Controller
    {
        private readonly RawgService _rawg;
        private readonly GameWikiDbContext _context;

        public GamesImportController(RawgService rawg, GameWikiDbContext context)
        {
            _rawg = rawg;
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1, string search = "")
        {
            var result = await _rawg.GetGamesAsync(page, 20, search);
            ViewBag.Page = page;
            ViewBag.Search = search;
            ViewBag.HasNext = result.Next != null;
            return View(result.Results);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(int rawgId)
        {
            
            var details = await _rawg.GetGameDetailsAsync(rawgId);

            
            var exists = await _context.Games.AnyAsync(g => g.Title == details.Name);
            if (exists)
            {
                TempData["ImportError"] = $"Gra \"{details.Name}\" już istnieje w bazie.";
                return RedirectToAction(nameof(Index));
            }

            
            DateTime releaseDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(details.Released))
                DateTime.TryParse(details.Released, out releaseDate);

            var game = new Game
            {
                Title = details.Name,
                Description = details.DescriptionRaw ?? "Brak opisu.",
                ReleaseDate = releaseDate,
                BackgroundImage = details.BackgroundImage,
                RawgRating = details.Rating,
                RawgRatingsCount = details.RatingsCount,
                GameGenres = new List<GameGenre>(),
                GamePlatforms = new List<GamePlatform>()
            };


            if (details.Genres != null)
            {
                foreach (var rawgGenre in details.Genres)
                {
                    var genre = await _context.Genres.FirstOrDefaultAsync(g => g.Name == rawgGenre.Name)
                                ?? new Genre { Name = rawgGenre.Name };

                    if (genre.Id == 0)
                        _context.Genres.Add(genre);

                    game.GameGenres.Add(new GameGenre { Genre = genre });
                }
            }

            
            if (details.Platforms != null)
            {
                foreach (var wrapper in details.Platforms)
                {
                    var p = wrapper.Platform;
                    var platform = await _context.Platforms.FirstOrDefaultAsync(x => x.Name == p.Name)
                                   ?? new Platform { Name = p.Name };

                    if (platform.Id == 0)
                        _context.Platforms.Add(platform);

                    game.GamePlatforms.Add(new GamePlatform { Platform = platform });
                }
            }

            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            TempData["ImportSuccess"] = $"Zaimportowano: {game.Title}";
            return RedirectToAction(nameof(Index));
        }
    }
}