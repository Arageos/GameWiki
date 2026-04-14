namespace GameWiki.Models
{
    public class Article
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public Game Game { get; set; }

        public string Title { get; set; }
        public string Content { get; set; }

        public string Status { get; set; } // Draft / Approved
    }
}
