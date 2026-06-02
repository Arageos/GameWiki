using GameWiki.Models;

namespace GameWiki.DTOs.Game
{
    public class GameIndexViewModel
    {
        public IEnumerable<GameDto> Games { get; set; }
        public List<Genre> Genres { get; set; }
        public List<Platform> Platforms { get; set; }
        public string? Search { get; set; }
        public int? GenreId { get; set; }
        public int? PlatformId { get; set; }
        public bool IsFiltered => !string.IsNullOrEmpty(Search) || GenreId.HasValue || PlatformId.HasValue;
    }
}