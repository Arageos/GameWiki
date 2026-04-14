namespace GameWiki.Models
{
    public class Game
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime ReleaseDate { get; set; }

        public ICollection<GameGenre> GameGenres { get; set; }
        public ICollection<GamePlatform> GamePlatforms { get; set; }
    }
}
