using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? ProfilePictureUrl { get; set; }

        public ICollection<Review> Reviews { get; set; }
        public ICollection<FavoriteList> FavoriteLists { get; set; }
        public bool IsBanned { get; set; } = false;
    }
}
