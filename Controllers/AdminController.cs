using GameWiki.DTOs.Admin;
using GameWiki.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    // Dostęp mają TYLKO Admini i Moderatorzy
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminController : Controller
    {
        private readonly GameWikiDbContext _context;

        public AdminController(GameWikiDbContext context)
        {
            _context = context;
        }

        // --- PANEL GŁÓWNY (Lista użytkowników) ---
        public async Task<IActionResult> Index()
        {
            // Pobieramy wszystkich użytkowników i mapujemy ich na naszego DTO
            var users = await _context.Users
                .Select(u => new UserListDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    IsBanned = u.IsBanned,
                    // Wyciągamy nazwę roli
                    RoleName = _context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Select(ur => ur.Role.Name)
                        .FirstOrDefault() ?? "User"
                })
                .OrderBy(u => u.RoleName)
                .ThenBy(u => u.Username)
                .ToListAsync();

            return View(users);
        }

        // --- BANOWANIE / ODBANOWANIE ---
        [HttpPost]
        public async Task<IActionResult> ToggleBan(int userId)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userId == currentUserId)
            {
                TempData["ErrorMessage"] = "Nie możesz zbanować samego siebie!";
                return RedirectToAction(nameof(Index));
            }

            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null) return NotFound();

            // Zabezpieczenie: Mod nie może zbanować Admina ani innego Moda
            var targetRole = await _context.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.Role.Name).FirstOrDefaultAsync() ?? "User";
            if (currentUserRole == "Moderator" && (targetRole == "Admin" || targetRole == "Moderator"))
            {
                TempData["ErrorMessage"] = "Moderator może banować tylko zwykłych użytkowników.";
                return RedirectToAction(nameof(Index));
            }

            // Przełączamy status bana
            targetUser.IsBanned = !targetUser.IsBanned;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = targetUser.IsBanned ? $"Użytkownik {targetUser.Username} został zbanowany." : $"Użytkownik {targetUser.Username} został odbanowany.";
            return RedirectToAction(nameof(Index));
        }

        // --- ZMIANA ROLI (TYLKO DLA ADMINA) ---
        [HttpPost]
        [Authorize(Roles = "Admin")] // Ta akcja wymaga ścisłego dostępu Admina
        public async Task<IActionResult> ToggleModRole(int userId)
        {
            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null) return NotFound();

            var userRoleObj = await _context.UserRoles.Include(ur => ur.Role).FirstOrDefaultAsync(ur => ur.UserId == userId);
            var currentRole = userRoleObj?.Role.Name ?? "User";

            // Nie ruszamy kont innych Adminów
            if (currentRole == "Admin")
            {
                TempData["ErrorMessage"] = "Nie można zmienić uprawnień Administratora.";
                return RedirectToAction(nameof(Index));
            }

            // Szukamy ról w bazie, żeby móc je przypisać
            var modRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Moderator");
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");

            if (userRoleObj != null)
            {
                // Usuwamy dotychczasową rolę
                _context.UserRoles.Remove(userRoleObj);
            }

            // Zmieniamy na Moda lub z powrotem na Usera
            if (currentRole == "User")
            {
                _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = modRole!.Id });
                TempData["SuccessMessage"] = $"Użytkownik {targetUser.Username} został awansowany na Moderatora.";
            }
            else if (currentRole == "Moderator")
            {
                _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = userRole!.Id });
                TempData["SuccessMessage"] = $"Moderator {targetUser.Username} został zdegradowany do roli Użytkownika.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> PendingArticles()
        {
            var articles = await _context.Articles
                .Where(a => !a.IsVerified)
                .Include(a => a.Author)
                .Include(a => a.Game)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            return View(articles);
        }
    }
}