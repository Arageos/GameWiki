using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public int? ParentCommentId { get; set; }
        public Comment ParentComment { get; set; }
    }
}
