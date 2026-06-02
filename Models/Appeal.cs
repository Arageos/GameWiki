namespace GameWiki.Models
{
    public enum AppealStatus { Pending, Approved, Rejected }

    public class Appeal
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public AppealStatus Status { get; set; } = AppealStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}