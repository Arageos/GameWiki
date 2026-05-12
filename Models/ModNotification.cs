namespace GameWiki.Models
{
    public class ModNotification
    {
        public int Id { get; set; }
        public string Message { get; set; } // np. "Użytkownik X dodał nowy artykuł"
        public string ActionUrl { get; set; } // Link do sprawdzenia treści/zgłoszenia
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; } = false; // Czy ktoś już się tym zajął
    }
}