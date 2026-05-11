using System.ComponentModel.DataAnnotations;

namespace GameWiki.DTOs.Auth
{
    public class UpdateProfileDto
    {
        [Required(ErrorMessage = "Nazwa użytkownika jest wymagana")]
        [StringLength(20, MinimumLength = 4, ErrorMessage = "Nazwa użytkownika musi mieć od 4 do 20 znaków")]
        [Display(Name = "Nazwa użytkownika")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Email jest wymagany")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format email")]
        [RegularExpression(@"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$", ErrorMessage = "Podaj poprawny adres email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [MaxLength(500, ErrorMessage = "Opis może mieć maksymalnie 500 znaków")]
        [Display(Name = "O mnie")]
        public string? Description { get; set; }
    }
}