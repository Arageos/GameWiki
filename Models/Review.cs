using System.ComponentModel.DataAnnotations;
namespace GameWiki.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public Game Game { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }
        [MaxLength(2000)]
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}