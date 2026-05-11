using System.ComponentModel.DataAnnotations;

namespace GameWiki.DTOs.Auth
{
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Obecne hasło jest wymagane")]
        [DataType(DataType.Password)]
        [Display(Name = "Obecne hasło")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "Nowe hasło jest wymagane")]
        [MinLength(8, ErrorMessage = "Hasło musi mieć co najmniej 8 znaków")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Hasło musi zawierać małą i dużą literę, cyfrę oraz znak specjalny.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nowe hasło")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Potwierdzenie hasła jest wymagane")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Hasła nie są identyczne")]
        [Display(Name = "Potwierdź nowe hasło")]
        public string ConfirmNewPassword { get; set; }
    }
}