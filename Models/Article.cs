using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class Article
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public Game Game { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public string Status { get; set; } // Draft / Approved
    }
}
