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
        [MaxLength(2000)]
        public string Description { get; set; }
        public DateTime ReleaseDate { get; set; }

        public ICollection<GameGenre> GameGenres { get; set; }
        public ICollection<GamePlatform> GamePlatforms { get; set; }
    }
}
