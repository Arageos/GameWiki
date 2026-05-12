namespace GameWiki.DTOs.Review
{

    public class CreateReviewDto
    {
        public int GameId { get; set; }
        [System.ComponentModel.DataAnnotations.Range(1, 5)]
        public int Rating { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(2000)]
        public string? Content { get; set; }
    }
}