using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class Article
    {
        public int Id { get; set; }

        public int GameId { get; set; }
        public Game Game { get; set; }

        public int AuthorId { get; set; }
        public User Author { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        public string? CoverImageUrl { get; set; }

        public bool IsVerified { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ArticleBlock> Blocks { get; set; }
        public ICollection<Comment> Comments { get; set; }
    }
}