using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class Game
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; }

        [Required]
        [MaxLength(10000)]
        public string Description { get; set; }
        public DateTime ReleaseDate { get; set; }

        public string? BackgroundImage { get; set; }
        public double? RawgRating { get; set; }
        public int? RawgRatingsCount { get; set; }

        public ICollection<GameGenre> GameGenres { get; set; }
        public ICollection<GamePlatform> GamePlatforms { get; set; }

        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
