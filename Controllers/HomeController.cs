using System.Diagnostics;
using GameWiki.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly GameWikiDbContext _context;

        public HomeController(ILogger<HomeController> logger, GameWikiDbContext context)
        {
            _logger  = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.GamesCount    = await _context.Games.CountAsync();
            ViewBag.ArticlesCount = await _context.Articles.CountAsync();
            ViewBag.ReviewsCount  = await _context.Reviews.CountAsync();

            ViewBag.FeaturedGames = await _context.Games
                .Where(g => g.BackgroundImage != null)
                .OrderBy(_ => Guid.NewGuid())
                .Take(6)
                .Select(g => new { g.Id, g.Title, g.BackgroundImage })
                .ToListAsync();

            ViewBag.Genres = await _context.Genres.OrderBy(g => g.Name).ToListAsync();

            return View();
        }

        public IActionResult Privacy() => View();
        public IActionResult Terms() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
            => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
