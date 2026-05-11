using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class FavoriteList
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } // <-- Powiązanie z Userem

        [Required(ErrorMessage = "Nazwa listy jest wymagana")]
        [MaxLength(50)]
        public string Name { get; set; }

        public ICollection<FavoriteGame> FavoriteGames { get; set; }
    }
}