using System.ComponentModel.DataAnnotations;

namespace GameWiki.DTOs.Auth
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Nazwa użytkownika jest wymagana")]
        [MaxLength(50)]
        [Display(Name = "Nazwa użytkownika")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Email jest wymagany")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format email")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Hasło jest wymagane")]
        [MinLength(6, ErrorMessage = "Hasło musi mieć co najmniej 6 znaków")]
        [DataType(DataType.Password)]
        [Display(Name = "Hasło")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Potwierdzenie hasła jest wymagane")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Hasła nie są identyczne")]
        [Display(Name = "Potwierdź hasło")]
        public string ConfirmPassword { get; set; }
    }
}