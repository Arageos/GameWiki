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
        public async Task<IActionResult> ToggleBan(int userId, string? banReason)
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

            var targetRole = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync() ?? "User";

            if (currentUserRole == "Moderator" && (targetRole == "Admin" || targetRole == "Moderator"))
            {
                TempData["ErrorMessage"] = "Moderator może banować tylko zwykłych użytkowników.";
                return RedirectToAction(nameof(Index));
            }

            targetUser.IsBanned = !targetUser.IsBanned;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = targetUser.IsBanned
                ? $"Użytkownik {targetUser.Username} został zbanowany."
                : $"Użytkownik {targetUser.Username} został odbanowany.";

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

            // Auto-resolve zgłoszeń dla usuniętej treści
            bool anyResolved = false;
            foreach (var r in reports.ToList())
            {
                bool targetExists = r.Type switch
                {
                    ReportType.Article => await _context.Articles.AnyAsync(a => a.Id == r.TargetId),
                    ReportType.Comment => await _context.Comments.AnyAsync(c => c.Id == r.TargetId),
                    ReportType.Review => await _context.Reviews.AnyAsync(rv => rv.Id == r.TargetId),
                    _ => true
                };

                if (!targetExists)
                {
                    r.Status = ReportStatus.Resolved;
                    reports.Remove(r);
                    anyResolved = true;
                }
            }
            if (anyResolved)
                await _context.SaveChangesAsync();

            var reportDtos = new List<ReportItemDto>();

            foreach (var r in reports)
            {
                var dto = new ReportItemDto
                {
                    ReportId = r.Id,
                    ReporterName = r.Reporter.Username,
                    Type = r.Type,
                    TargetId = r.TargetId,
                    Reason = r.Reason,
                    CreatedAt = r.CreatedAt
                };

                if (r.Type == ReportType.Comment)
                {
                    var comment = await _context.Comments.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == r.TargetId);
                    dto.ContentText = comment != null ? comment.Content : "[Treść została usunięta]";
                    dto.ContentAuthorName = comment?.User?.Username ?? "Nieznany";
                }
                else if (r.Type == ReportType.Review)
                {
                    var review = await _context.Reviews.Include(rv => rv.User).FirstOrDefaultAsync(rv => rv.Id == r.TargetId);
                    dto.ContentText = review != null ? review.Content! : "[Treść została usunięta]";
                    dto.ContentAuthorName = review?.User?.Username ?? "Nieznany";
                }

                reportDtos.Add(dto);
            }

            return View(reportDtos);
        }

        // --- ROZPATRYWANIE ZGŁOSZEŃ ---
        [HttpPost]
        public async Task<IActionResult> HandleReport(int reportId, string actionType, string? deleteReason)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null) return NotFound();

            if (actionType == "delete")
            {
                int? contentAuthorId = null;
                string contentLabel = "Twoja treść";

                if (report.Type == ReportType.Comment)
                {
                    var comment = await _context.Comments.FindAsync(report.TargetId);
                    if (comment != null)
                    {
                        contentAuthorId = comment.UserId;
                        contentLabel = "Twój komentarz";

                        var replies = await _context.Comments
                            .Where(c => c.ParentCommentId == comment.Id)
                            .ToListAsync();
                        if (replies.Any())
                            _context.Comments.RemoveRange(replies);

                        _context.Comments.Remove(comment);
                    }
                }
                else if (report.Type == ReportType.Article)
                {
                    var article = await _context.Articles.FindAsync(report.TargetId);
                    if (article != null)
                    {
                        contentAuthorId = article.AuthorId;
                        contentLabel = "Twój artykuł";
                        _context.Articles.Remove(article);
                    }
                }

                // Powiadomienie dla autora usuniętej treści
                if (contentAuthorId.HasValue)
                {
                    _context.UserNotifications.Add(new UserNotification
                    {
                        UserId = contentAuthorId.Value,
                        Type = NotificationType.ContentRemoved,
                        Message = $"{contentLabel} została usunięta przez moderację.",
                        Reason = string.IsNullOrWhiteSpace(deleteReason) ? "Naruszenie regulaminu." : deleteReason
                    });
                }

                TempData["SuccessMessage"] = "Treść została pomyślnie usunięta, a zgłoszenie zamknięte.";
            }
            else if (actionType == "dismiss")
            {
                TempData["SuccessMessage"] = "Zgłoszenie zostało odrzucone.";
            }

            report.Status = ReportStatus.Resolved;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Reports));
        }
        public async Task<IActionResult> PendingReviews()
        {
            var reviews = await _context.Reviews
                .Where(r => !r.IsVerified && !string.IsNullOrEmpty(r.Content))
                .Include(r => r.User)
                .Include(r => r.Game)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();

            return View(reviews);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyReview(int reviewId)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null) return NotFound();

            review.IsVerified = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Recenzja została zweryfikowana.";
            return RedirectToAction(nameof(PendingReviews));
        }
        // --- PANEL ODWOŁAŃ ---
        public async Task<IActionResult> Appeals()
        {
            var appeals = await _context.Appeals
                .Include(a => a.User)
                .Where(a => a.Status == AppealStatus.Pending) // Pokazuj tylko nierozpatrzone
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            return View(appeals);
        }

        [HttpPost]
        public async Task<IActionResult> HandleAppeal(int appealId, string actionType)
        {
            var appeal = await _context.Appeals
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == appealId);

            if (appeal == null) return NotFound();

            if (actionType == "approve")
            {
                appeal.Status = AppealStatus.Approved;

                // 1. AUTOMATYCZNE ODBANOWANIE (jeśli odwołanie dotyczyło blokady konta)
                if (appeal.User != null && appeal.User.IsBanned)
                {
                    appeal.User.IsBanned = false;
                }

                TempData["SuccessMessage"] = $"Odwołanie użytkownika {appeal.User?.Username} zostało zaakceptowane. Konto zostało pomyślnie odbanowane.";
            }
            else if (actionType == "reject")
            {
                appeal.Status = AppealStatus.Rejected;
                TempData["SuccessMessage"] = $"Odwołanie użytkownika {appeal.User?.Username} zostało odrzucone.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Appeals));
        }
    }
}