using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class Genre
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        public ICollection<GameGenre> GameGenres { get; set; }
    }
}
