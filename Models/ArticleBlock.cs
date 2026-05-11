namespace GameWiki.Models
{
    public enum ArticleBlockType
    {
        Text,
        Image
    }

    public class ArticleBlock
    {
        public int Id { get; set; }

        public int ArticleId { get; set; }
        public Article Article { get; set; }

        public ArticleBlockType Type { get; set; }

        public string? Content { get; set; }

        public int Order { get; set; }
    }
}