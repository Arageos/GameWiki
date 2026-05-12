namespace GameWiki.DTOs.Review
{
    public class ReviewDto
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public string GameTitle { get; set; }
        public string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public int Rating { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOwner { get; set; }
    }
}