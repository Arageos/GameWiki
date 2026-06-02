using GameWiki.DTOs.Admin;
using GameWiki.Models;
using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminController : Controller
    {
        private readonly GameWikiDbContext _context;
        private readonly ArticleService _articleService;

        public AdminController(GameWikiDbContext context, ArticleService articleService)
        {
            _context = context;
            _articleService = articleService;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .Select(u => new UserListDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    IsBanned = u.IsBanned,
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

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ToggleModRole(int userId)
        {
            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null) return NotFound();

            var userRoleObj = await _context.UserRoles.Include(ur => ur.Role).FirstOrDefaultAsync(ur => ur.UserId == userId);
            var currentRole = userRoleObj?.Role.Name ?? "User";

            if (currentRole == "Admin")
            {
                TempData["ErrorMessage"] = "Nie można zmienić uprawnień Administratora.";
                return RedirectToAction(nameof(Index));
            }

            var modRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Moderator");
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");

            if (userRoleObj != null)
                _context.UserRoles.Remove(userRoleObj);

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

            _context.UserNotifications.Add(new UserNotification
            {
                UserId = review.UserId,
                Type = NotificationType.ContentEdited,
                Message = "Twoja recenzja została zweryfikowana i jest teraz widoczna publicznie.",
                ActionUrl = $"/Reviews/Index?gameId={review.GameId}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Recenzja została zweryfikowana.";
            return RedirectToAction(nameof(PendingReviews));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteReview(int reviewId, string? deleteReason)
        {
            var review = await _context.Reviews
                .Include(r => r.Game)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null) return NotFound();

            _context.UserNotifications.Add(new UserNotification
            {
                UserId = review.UserId,
                Type = NotificationType.ContentRemoved,
                Message = $"Twoja recenzja gry {review.Game?.Title} została odrzucona przez moderację.",
                Reason = string.IsNullOrWhiteSpace(deleteReason) ? "Treść nie spełnia wymogów serwisu." : deleteReason,
                CreatedAt = DateTime.UtcNow
            });

            // Zamknij powiązane zgłoszenia
            var reports = await _context.Reports
                .Where(r => r.Type == ReportType.Review && r.TargetId == reviewId && r.Status == ReportStatus.Pending)
                .ToListAsync();
            foreach (var r in reports) r.Status = ReportStatus.Resolved;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Recenzja została odrzucona i autor został powiadomiony.";
            return RedirectToAction(nameof(PendingReviews));
        }

        // ── Zgłoszenia ───────────────────────────────────────────────────────
        public async Task<IActionResult> Reports()
        {
            var reports = await _context.Reports
                .Include(r => r.Reporter)
                .Where(r => r.Status == ReportStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

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
            if (anyResolved) await _context.SaveChangesAsync();

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
                    dto.ContentText = comment?.Content ?? "[Treść usunięta]";
                    dto.ContentAuthorName = comment?.User?.Username ?? "Nieznany";
                }
                else if (r.Type == ReportType.Review)
                {
                    var review = await _context.Reviews.Include(rv => rv.User).FirstOrDefaultAsync(rv => rv.Id == r.TargetId);
                    dto.ContentText = review?.Content ?? "[Treść usunięta]";
                    dto.ContentAuthorName = review?.User?.Username ?? "Nieznany";
                }

                reportDtos.Add(dto);
            }

            return View(reportDtos);
        }

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
                        var replies = await _context.Comments.Where(c => c.ParentCommentId == comment.Id).ToListAsync();
                        if (replies.Any()) _context.Comments.RemoveRange(replies);
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
                else if (report.Type == ReportType.Review)
                {
                    var review = await _context.Reviews.Include(r => r.Game).FirstOrDefaultAsync(r => r.Id == report.TargetId);
                    if (review != null)
                    {
                        contentAuthorId = review.UserId;
                        contentLabel = $"Twoja recenzja gry {review.Game?.Title}";
                        _context.Reviews.Remove(review);
                    }
                }

                if (contentAuthorId.HasValue)
                {
                    _context.UserNotifications.Add(new UserNotification
                    {
                        UserId = contentAuthorId.Value,
                        Type = NotificationType.ContentRemoved,
                        Message = $"{contentLabel} została usunięta przez moderację.",
                        Reason = string.IsNullOrWhiteSpace(deleteReason) ? "Naruszenie regulaminu." : deleteReason,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                TempData["SuccessMessage"] = "Treść została usunięta i zgłoszenie zamknięte.";
            }
            else
            {
                TempData["SuccessMessage"] = "Zgłoszenie zostało odrzucone.";
            }

            report.Status = ReportStatus.Resolved;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Reports));
        }

        public async Task<IActionResult> Appeals()
        {
            var appeals = await _context.Appeals
                .Include(a => a.User)
                .Where(a => a.Status == AppealStatus.Pending)
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
                if (appeal.User != null && appeal.User.IsBanned)
                    appeal.User.IsBanned = false;

                TempData["SuccessMessage"] = $"Odwołanie użytkownika {appeal.User?.Username} zostało zaakceptowane.";
            }
            else
            {
                appeal.Status = AppealStatus.Rejected;
                TempData["SuccessMessage"] = $"Odwołanie użytkownika {appeal.User?.Username} zostało odrzucone.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Appeals));
        }
    }
}