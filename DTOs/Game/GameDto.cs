namespace GameWiki.DTOs.Game
{
    public class GameDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime ReleaseDate { get; set; }

        public string? BackgroundImage { get; set; }
        public double? RawgRating { get; set; }
        public int? RawgRatingsCount { get; set; }
        public double? LocalRating { get; set; }
        public int? LocalRatingsCount { get; set; }
    }
}
