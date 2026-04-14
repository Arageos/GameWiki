namespace GameWiki.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }

        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public int? ParentCommentId { get; set; }
        public Comment ParentComment { get; set; }
    }
}
