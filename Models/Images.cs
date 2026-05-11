namespace GameWiki.Models
{
    public class Image
    {
        public int Id { get; set; }
        public string Url { get; set; }

        public int? GameId { get; set; }
        public int? ArticleBlockId { get; set; }
    }
}