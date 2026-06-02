namespace GameWiki.Models
{
    public class ModNotification
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public string ActionUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsResolved { get; set; } = false;
    }
}