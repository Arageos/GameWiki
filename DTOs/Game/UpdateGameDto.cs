using System.ComponentModel.DataAnnotations;

namespace GameWiki.DTOs.Game
{
    public class UpdateGameDto
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; }

        [Required]
        public string Description { get; set; }

        public DateTime ReleaseDate { get; set; }
    }
}
