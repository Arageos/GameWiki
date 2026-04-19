using System.ComponentModel.DataAnnotations;

namespace GameWiki.Models
{
    public class Platform
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; }

        public ICollection<GamePlatform> GamePlatforms { get; set; }
    }
}
