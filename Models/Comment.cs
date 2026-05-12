using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class Comment
    {
        public int Id { get; set; }

        public int ArticleId { get; set; }
        public Article Article { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? ParentCommentId { get; set; }
        public Comment ParentComment { get; set; }

        public ICollection<CommentReaction> Reactions { get; set; }
        public bool IsVerified { get; set; } = false;
    }
}