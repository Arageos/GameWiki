using GameWiki.DTOs.Game;
using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Services
{
    public class GameService
    {
        private readonly GameWikiDbContext _context;

        public GameService(GameWikiDbContext context)
        {
            _context = context;
        }

        public async Task<List<GameDto>> GetFilteredGamesAsync(string? search, int? genreId, int? platformId)
        {
            var query = _context.Games.AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(g => g.Title.Contains(search));

            if (genreId.HasValue)
                query = query.Where(g => g.GameGenres.Any(gg => gg.GenreId == genreId));

            if (platformId.HasValue)
                query = query.Where(g => g.GamePlatforms.Any(gp => gp.PlatformId == platformId));

            return await query
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
                .ToListAsync();
        }

        public async Task<List<Genre>> GetGenresAsync()
            => await _context.Genres.OrderBy(g => g.Name).ToListAsync();

        public async Task<List<Platform>> GetPlatformsAsync()
            => await _context.Platforms.OrderBy(p => p.Name).ToListAsync();

        
    }
}