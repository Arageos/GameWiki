namespace GameWiki.Models
{
    public class FavoriteGame
    {
        public int FavoriteListId { get; set; }
        public FavoriteList FavoriteList { get; set; } // <-- Powiązanie

        public int GameId { get; set; }
        public Game Game { get; set; } // <-- Powiązanie
    }
}