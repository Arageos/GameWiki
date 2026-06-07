using GameWiki.DTOs.Auth;
using GameWiki.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountService _accounts;
        private readonly NotificationService _notifications;

        public AccountController(AccountService accounts, NotificationService notifications)
        {
            _accounts      = accounts;
            _notifications = notifications;
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

            var (user, field, error) = await _accounts.RegisterAsync(dto);

            if (user == null)
            {
                ModelState.AddModelError(field!, error!);
                return View(dto);
            }

            var role = await _accounts.GetRoleNameAsync(user.Id);
            await SignInUserAsync(user.Id, user.Username, user.Email,
                                  role, user.ProfilePictureUrl, rememberMe: false);

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

            var user = await _accounts.ValidateCredentialsAsync(dto.Email, dto.Password);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Nieprawidłowy email lub hasło");
                return View(dto);
            }

            if (user.IsBanned)
            {
                var (reason, appealStatus) = await _accounts.GetBanInfoAsync(user);
                ModelState.AddModelError(string.Empty,
                    $"Twoje konto zostało zablokowane. Skontaktuj się z Administracją. Powód: {reason}");
                ViewBag.BannedEmail    = user.Email;
                ViewBag.BanAppealStatus = appealStatus;
                return View(dto);
            }

            var role = await _accounts.GetRoleNameAsync(user.Id);
            await SignInUserAsync(user.Id, user.Username, user.Email,
                                  role, user.ProfilePictureUrl, dto.RememberMe);

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

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userId = GetUserId();
            var user   = await _accounts.GetProfileAsync(userId);
            if (user == null) return NotFound();

            ViewBag.AllGames      = await _accounts.GetAllGamesForDropdownAsync();
            ViewBag.RoleName      = await _accounts.GetRoleNameAsync(userId);
            ViewBag.UserArticles  = await _accounts.GetUserArticlesAsync(userId);

            return View(user);
        }

        [Authorize]
        public async Task<IActionResult> Settings()
        {
            var userId = GetUserId();
            var user   = await _accounts.FindByIdAsync(userId);
            if (user == null) return NotFound();

            ViewBag.ProfilePictureUrl = user.ProfilePictureUrl;

            return View(new UpdateProfileDto
            {
                Username    = user.Username,
                Email       = user.Email,
                Description = user.Description
            });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto)
        {
            var userId = GetUserId();
            var user   = await _accounts.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.ProfilePictureUrl = user.ProfilePictureUrl;
                return View("Settings", dto);
            }

            var (field, error) = await _accounts.UpdateProfileAsync(userId, dto);

            if (field != null)
            {
                ModelState.AddModelError(field, error!);
                ViewBag.ProfilePictureUrl = user.ProfilePictureUrl;
                return View("Settings", dto);
            }

            var role = await _accounts.GetRoleNameAsync(userId);
            var updated = await _accounts.FindByIdAsync(userId);
            await SignInUserAsync(updated!.Id, updated.Username, updated.Email,
                                  role, updated.ProfilePictureUrl, rememberMe: true);

            TempData["SuccessMessage"] = "Profil został zaktualizowany.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var userId = GetUserId();

            if (!ModelState.IsValid)
            {
                var u = await _accounts.FindByIdAsync(userId);
                return View("Settings", new UpdateProfileDto
                    { Username = u!.Username, Email = u.Email, Description = u.Description });
            }

            var ok = await _accounts.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);

            if (!ok)
            {
                TempData["ErrorMessage"] = "Obecne hasło jest nieprawidłowe.";
                return RedirectToAction(nameof(Settings));
            }

            TempData["SuccessMessage"] = "Hasło zostało pomyślnie zmienione.";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
                return RedirectToAction(nameof(Settings));

            var userId = GetUserId();
            var (url, error) = await _accounts.UploadAvatarAsync(userId, avatarFile);

            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(Settings));
            }

            var user = await _accounts.FindByIdAsync(userId);
            var role = await _accounts.GetRoleNameAsync(userId);
            await SignInUserAsync(user!.Id, user.Username, user.Email,
                                  role, url, rememberMe: true);

            TempData["SuccessMessage"] = "Awatar został zaktualizowany.";
            return RedirectToAction(nameof(Settings));
        }

        [Authorize]
        public async Task<IActionResult> Notifications()
        {
            var notifications = await _notifications.GetUserNotificationsAsync(GetUserId());
            return View(notifications);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            await _notifications.DeleteAsync(id, GetUserId());
            TempData["SuccessMessage"] = "Powiadomienie zostało usunięte.";
            return RedirectToAction(nameof(Notifications));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MarkAllRead()
        {
            await _notifications.MarkAllReadAsync(GetUserId());
            return RedirectToAction(nameof(Notifications));
        }

        private int GetUserId()
            => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        private async Task SignInUserAsync(int id, string username, string email,
                                           string role, string? avatarUrl, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, id.ToString()),
                new(ClaimTypes.Name,  username),
                new(ClaimTypes.Email, email),
                new(ClaimTypes.Role,  role),
                new("AvatarUrl",      avatarUrl ?? "")
            };

            var identity   = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var properties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc   = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                properties);
        }
    }
}
