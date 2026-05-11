namespace GameWiki.Models
{
    public enum ReactionType
    {
        Like,
        Dislike
    }

    public class CommentReaction
    {
        public int Id { get; set; }

        public int CommentId { get; set; }
        public Comment Comment { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        public ReactionType Type { get; set; }
    }
}