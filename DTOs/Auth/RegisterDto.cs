using System.ComponentModel.DataAnnotations;

namespace GameWiki.DTOs.Auth
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Nazwa użytkownika jest wymagana")]
        [StringLength(20, MinimumLength = 4, ErrorMessage = "Nazwa użytkownika musi mieć od 4 do 20 znaków")]
        [Display(Name = "Nazwa użytkownika")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Email jest wymagany")]
        [EmailAddress(ErrorMessage = "Nieprawidłowy format email")]
        [RegularExpression(@"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$", ErrorMessage = "Podaj poprawny adres email (np. jan@domena.pl)")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Hasło jest wymagane")]
        [MinLength(8, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", ErrorMessage = "Hasło musi zawierać małą i dużą literę, cyfrę oraz znak specjalny (@, $, !, %, *, ?, &)")]
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