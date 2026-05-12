using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public enum ReportType { User, Article, Review, Comment }
    public enum ReportStatus { Pending, Resolved, Dismissed }

    public class Report
    {
        public int Id { get; set; }

        public int ReporterId { get; set; }
        public User Reporter { get; set; }

        public ReportType Type { get; set; }

        // Przechowuje ID zgłaszanego elementu (ID artykułu, komentarza, recenzji lub użytkownika)
        public int TargetId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; }

        public ReportStatus Status { get; set; } = ReportStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}