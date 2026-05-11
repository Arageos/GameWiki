using GameWiki.DTOs.Auth;
using GameWiki.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    public class AccountController : Controller
    {
        private readonly GameWikiDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AccountController(GameWikiDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Account/Register
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            // Sprawdź czy email już istnieje
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                ModelState.AddModelError("Email", "Ten email jest już zajęty");
                return View(dto);
            }

            // Sprawdź czy nazwa użytkownika już istnieje
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
            {
                ModelState.AddModelError("Username", "Ta nazwa użytkownika jest już zajęta");
                return View(dto);
            }

            // Pobierz lub utwórz rolę "User"
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole == null)
            {
                userRole = new Role { Name = "User" };
                _context.Roles.Add(userRole);
                await _context.SaveChangesAsync();
            }

            // Utwórz użytkownika
            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Przypisz rolę
            _context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = userRole.Id
            });
            await _context.SaveChangesAsync();

            // Zaloguj od razu po rejestracji
            await SignInUser(user, userRole.Name, false);

            return RedirectToAction("Index", "Home");
        }

        // GET: Account/Login
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto dto, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Nieprawidłowy email lub hasło");
                return View(dto);
            }

            if (user.IsBanned)
            {
                ModelState.AddModelError(string.Empty, "Zostałeś zablokowany. Skontaktuj się z administracją.");
                return View(dto);
            }

            // Pobierz rolę użytkownika
            var userRole = await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync() ?? "User";

            await SignInUser(user, userRole, dto.RememberMe);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // POST: Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // GET: Account/AccessDenied
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Pomocnicza metoda do logowania
        private async Task SignInUser(User user, string role, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, role),
                new Claim("AvatarUrl", user.ProfilePictureUrl ?? "")
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return NotFound();

            int userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users
                .Include(u => u.Reviews)
                .Include(u => u.FavoriteLists)
                    .ThenInclude(fl => fl.FavoriteGames)
                    .ThenInclude(fg => fg.Game)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound();

            ViewBag.AllGames = await _context.Games
                .OrderBy(g => g.Title)
                .Select(g => new { g.Id, g.Title })
                .ToListAsync();

            var role = await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync() ?? "Użytkownik";

            ViewBag.RoleName = role;

            return View(user);
        }
        [Authorize]
        public async Task<IActionResult> Settings()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return NotFound();

            var dto = new UpdateProfileDto
            {
                Username = user.Username,
                Email = user.Email,
                Description = user.Description
            };

            ViewBag.ProfilePictureUrl = user.ProfilePictureUrl;
            return View(dto);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.ProfilePictureUrl = user.ProfilePictureUrl;
                return View("Settings", dto);
            }

            // Sprawdzanie unikalności emaila/loginu jeśli zostały zmienione
            if (dto.Email != user.Email && await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                ModelState.AddModelError("Email", "Ten email jest już zajęty");
                return View("Settings", dto);
            }

            if (dto.Username != user.Username && await _context.Users.AnyAsync(u => u.Username == dto.Username))
            {
                ModelState.AddModelError("Username", "Ta nazwa użytkownika jest już zajęta");
                return View("Settings", dto);
            }

            user.Username = dto.Username;
            user.Email = dto.Email;
            user.Description = dto.Description;

            await _context.SaveChangesAsync();

            // Konieczność przelogowania, aby odświeżyć Claimsy z nową nazwą/emailem
            var role = await _context.UserRoles.Include(ur => ur.Role).Where(ur => ur.UserId == user.Id).Select(ur => ur.Role.Name).FirstOrDefaultAsync() ?? "User";
            await SignInUser(user, role, true); // Odświeżenie ciasteczka

            TempData["SuccessMessage"] = "Profil został zaktualizowany.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var user = await _context.Users.FindAsync(userId);

            if (!ModelState.IsValid) return View("Settings", new UpdateProfileDto { Username = user!.Username, Email = user.Email, Description = user.Description });

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user!.PasswordHash))
            {
                TempData["ErrorMessage"] = "Obecne hasło jest nieprawidłowe.";
                return RedirectToAction(nameof(Settings));
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Hasło zostało pomyślnie zmienione.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            if (avatarFile != null && avatarFile.Length > 0)
            {
                // Sprawdzenie rozszerzenia (tylko png/jpg)
                var ext = Path.GetExtension(avatarFile.FileName).ToLower();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                {
                    TempData["ErrorMessage"] = "Dozwolone są tylko pliki .png, .jpg i .jpeg";
                    return RedirectToAction(nameof(Settings));
                }

                // Generowanie unikalnej nazwy i ścieżki
                var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "avatars");
                Directory.CreateDirectory(uploadsFolder); // Tworzy folder, jeśli nie istnieje

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + avatarFile.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var user = await _context.Users.FindAsync(userId);

                user!.ProfilePictureUrl = $"/images/avatars/{uniqueFileName}";
                await _context.SaveChangesAsync();
                var role = await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync() ?? "User";

                // Ponowne wywołanie SignInUser odświeży dane w ciasteczku (w tym nasz nowy AvatarUrl)
                await SignInUser(user, role, true);

                TempData["SuccessMessage"] = "Awatar został zaktualizowany.";
                return RedirectToAction(nameof(Settings));
            }
            return RedirectToAction(nameof(Settings));
        }
    }
}