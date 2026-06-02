namespace GameWiki.Models
{
    public enum NotificationType
    {
        Ban,
        Unban,
        ContentRemoved,
        NewArticle,
        NewReview,
        NewReport,
        NewGame,
        ContentEdited
    }

    public class UserNotification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public NotificationType Type { get; set; }
        public string Message { get; set; }
        public string? Reason { get; set; }
        public string? ActionUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}