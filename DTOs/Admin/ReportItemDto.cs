namespace GameWiki.DTOs.Admin
{
    public class ReportItemDto
    {
        public int ReportId { get; set; }
        public string ReporterName { get; set; }
        public GameWiki.Models.ReportType Type { get; set; }
        public int TargetId { get; set; }
        public string Reason { get; set; }
        public DateTime CreatedAt { get; set; }

        // Pola specyficzne dla recenzji i komentarzy
        public string ContentAuthorName { get; set; }
        public string ContentText { get; set; }
    }
}