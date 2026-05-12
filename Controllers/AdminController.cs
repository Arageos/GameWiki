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
        // --- PANEL ZGŁOSZEŃ ---
        public async Task<IActionResult> Reports()
        {
            var reports = await _context.Reports
                .Include(r => r.Reporter)
                .Where(r => r.Status == ReportStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var reportDtos = new List<GameWiki.DTOs.Admin.ReportItemDto>();

            foreach (var r in reports)
            {
                var dto = new GameWiki.DTOs.Admin.ReportItemDto
                {
                    ReportId = r.Id,
                    ReporterName = r.Reporter.Username,
                    Type = r.Type,
                    TargetId = r.TargetId,
                    Reason = r.Reason,
                    CreatedAt = r.CreatedAt
                };

                // Dociągamy treść w zależności od tego, co zgłoszono
                if (r.Type == ReportType.Comment)
                {
                    var comment = await _context.Comments.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == r.TargetId);
                    dto.ContentText = comment != null ? comment.Content : "[Treść została usunięta]";
                    dto.ContentAuthorName = comment?.User?.Username ?? "Nieznany";
                }
                else if (r.Type == ReportType.Review)
                {
                    var review = await _context.Reviews.Include(rev => rev.User).FirstOrDefaultAsync(rev => rev.Id == r.TargetId);
                    dto.ContentText = review != null ? review.Content : "[Treść została usunięta]";
                    dto.ContentAuthorName = review?.User?.Username ?? "Nieznany";
                }

                reportDtos.Add(dto);
            }

            return View(reportDtos);
        }

        // --- ROZPATRYWANIE ZGŁOSZEŃ ---
        [HttpPost]
        public async Task<IActionResult> HandleReport(int reportId, string actionType)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null) return NotFound();

            if (actionType == "delete")
            {
                // Usuwamy złą treść
                if (report.Type == ReportType.Comment)
                {
                    var comment = await _context.Comments.FindAsync(report.TargetId);
                    if (comment != null)
                    {
                        // 1. Najpierw znajdujemy i usuwamy wszystkie odpowiedzi do tego komentarza
                        var replies = await _context.Comments.Where(c => c.ParentCommentId == comment.Id).ToListAsync();
                        if (replies.Any())
                        {
                            _context.Comments.RemoveRange(replies);
                        }

                        // 2. Dopiero potem usuwamy główny komentarz
                        _context.Comments.Remove(comment);
                    }
                }
                else if (report.Type == ReportType.Review)
                {
                    var review = await _context.Reviews.FindAsync(report.TargetId);
                    if (review != null) _context.Reviews.Remove(review);
                }

                TempData["SuccessMessage"] = "Treść została pomyślnie usunięta, a zgłoszenie zamknięte.";
            }
            else if (actionType == "dismiss")
            {
                TempData["SuccessMessage"] = "Zgłoszenie zostało odrzucone.";
            }

            // Oznaczamy zgłoszenie jako załatwione
            report.Status = ReportStatus.Resolved;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Reports));
        }
    }
}