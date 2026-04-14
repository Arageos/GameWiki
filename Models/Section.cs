namespace GameWiki.Models
{
    public class Section
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public Article Article { get; set; }

        public string Title { get; set; }
        public string Content { get; set; }
        public int Order { get; set; }
    }
}
