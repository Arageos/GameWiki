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

        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                ModelState.AddModelError("Email", "Ten email jest już zajęty");
                return View(dto);
            }
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
            {
                ModelState.AddModelError("Username", "Ta nazwa użytkownika jest już zajęta");
                return View(dto);
            }

            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole == null)
            {
                userRole = new Role { Name = "User" };
                _context.Roles.Add(userRole);
                await _context.SaveChangesAsync();
            }

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
            await _context.SaveChangesAsync();

            await SignInUser(user, userRole.Name, false);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto dto, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(dto);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

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

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
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

            // Artykuły użytkownika
            ViewBag.UserArticles = await _context.Articles
                .Include(a => a.Game)
                .Where(a => a.AuthorId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.IsVerified,
                    a.CreatedAt,
                    GameTitle = a.Game.Title,
                    GameId = a.GameId
                })
                .ToListAsync();

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

            var role = await _context.UserRoles.Include(ur => ur.Role)
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync() ?? "User";
            await SignInUser(user, role, true);

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

            if (!ModelState.IsValid)
                return View("Settings", new UpdateProfileDto { Username = user!.Username, Email = user.Email, Description = user.Description });

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
                var ext = Path.GetExtension(avatarFile.FileName).ToLower();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                {
                    TempData["ErrorMessage"] = "Dozwolone są tylko pliki .png, .jpg i .jpeg";
                    return RedirectToAction(nameof(Settings));
                }

                var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "avatars");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + avatarFile.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                    await avatarFile.CopyToAsync(fileStream);

                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
                var user = await _context.Users.FindAsync(userId);

                user!.ProfilePictureUrl = $"/images/avatars/{uniqueFileName}";
                await _context.SaveChangesAsync();

                var role = await _context.UserRoles.Include(ur => ur.Role)
                    .Where(ur => ur.UserId == user.Id)
                    .Select(ur => ur.Role.Name)
                    .FirstOrDefaultAsync() ?? "User";

                await SignInUser(user, role, true);

                TempData["SuccessMessage"] = "Awatar został zaktualizowany.";
                return RedirectToAction(nameof(Settings));
            }
            return RedirectToAction(nameof(Settings));
        }
    }
}