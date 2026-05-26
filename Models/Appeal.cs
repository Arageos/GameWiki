namespace GameWiki.Models
{
    public enum AppealStatus { Pending, Approved, Rejected }

    public class Appeal
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        public string Subject { get; set; } // np. "Odwołanie od zablokowania konta" lub "Usunięcie recenzji"
        public string Message { get; set; } // Treść, w której użytkownik się tłumaczy

        public AppealStatus Status { get; set; } = AppealStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}