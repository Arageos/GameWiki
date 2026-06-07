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
        private readonly NotificationService _notifications;
        private readonly ReviewService _reviews;

        public AdminController(GameWikiDbContext context,
                               NotificationService notifications,
                               ReviewService reviews)
        {
            _context       = context;
            _notifications = notifications;
            _reviews       = reviews;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .Select(u => new UserListDto
                {
                    Id       = u.Id,
                    Username = u.Username,
                    Email    = u.Email,
                    IsBanned = u.IsBanned,
                    RoleName = _context.UserRoles
                        .Where(ur => ur.UserId == u.Id)
                        .Select(ur => ur.Role.Name)
                        .FirstOrDefault() ?? "User"
                })
                .OrderBy(u => u.RoleName).ThenBy(u => u.Username)
                .ToListAsync();

            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleBan(int userId, string? banReason)
        {
            var currentUserId   = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userId == currentUserId)
            {
                TempData["ErrorMessage"] = "Nie możesz zbanować samego siebie!";
                return RedirectToAction(nameof(Index));
            }

            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null) return NotFound();

            var targetRole = await _context.UserRoles
                .Where(ur => ur.UserId == userId).Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync() ?? "User";

            if (currentUserRole == "Moderator" && (targetRole == "Admin" || targetRole == "Moderator"))
            {
                TempData["ErrorMessage"] = "Moderator może banować tylko zwykłych użytkowników.";
                return RedirectToAction(nameof(Index));
            }

            targetUser.IsBanned = !targetUser.IsBanned;

            _notifications.NotifyUser(
                userId,
                targetUser.IsBanned ? NotificationType.Ban : NotificationType.Unban,
                targetUser.IsBanned
                    ? "Twoje konto zostało zablokowane przez administrację."
                    : "Twoje konto zostało odblokowane przez administrację.",
                reason: targetUser.IsBanned ? banReason : null
            );

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

            var userRoleObj = await _context.UserRoles
                .Include(ur => ur.Role)
                .FirstOrDefaultAsync(ur => ur.UserId == userId);
            var currentRole = userRoleObj?.Role.Name ?? "User";

            if (currentRole == "Admin")
            {
                TempData["ErrorMessage"] = "Nie można zmienić uprawnień Administratora.";
                return RedirectToAction(nameof(Index));
            }

            var modRole  = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Moderator");
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");

            if (userRoleObj != null) _context.UserRoles.Remove(userRoleObj);

            if (currentRole == "User")
            {
                _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = modRole!.Id });
                TempData["SuccessMessage"] = $"{targetUser.Username} awansował na Moderatora.";
            }
            else
            {
                _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = userRole!.Id });
                TempData["SuccessMessage"] = $"{targetUser.Username} zdegradowany do Użytkownika.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> PendingArticles()
        {
            var articles = await _context.Articles
                .Where(a => !a.IsVerified)
                .Include(a => a.Author).Include(a => a.Game)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();
            return View(articles);
        }

        public async Task<IActionResult> PendingReviews()
            => View(await _reviews.GetPendingAsync());

        [HttpPost]
        public async Task<IActionResult> VerifyReview(int reviewId)
        {
            await _reviews.VerifyAsync(reviewId);
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

            await _reviews.DeleteByModAsync(review, deleteReason);
            TempData["SuccessMessage"] = "Recenzja została odrzucona i autor powiadomiony.";
            return RedirectToAction(nameof(PendingReviews));
        }

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
                bool exists = r.Type switch
                {
                    ReportType.Article => await _context.Articles.AnyAsync(a => a.Id == r.TargetId),
                    ReportType.Comment => await _context.Comments.AnyAsync(c => c.Id == r.TargetId),
                    ReportType.Review  => await _context.Reviews.AnyAsync(rv => rv.Id == r.TargetId),
                    _                  => true
                };
                if (!exists) { r.Status = ReportStatus.Resolved; reports.Remove(r); anyResolved = true; }
            }
            if (anyResolved) await _context.SaveChangesAsync();

            var dtos = new List<ReportItemDto>();
            foreach (var r in reports)
            {
                var dto = new ReportItemDto
                {
                    ReportId     = r.Id,
                    ReporterName = r.Reporter.Username,
                    Type         = r.Type,
                    TargetId     = r.TargetId,
                    Reason       = r.Reason,
                    CreatedAt    = r.CreatedAt
                };

                if (r.Type == ReportType.Comment)
                {
                    var c = await _context.Comments.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == r.TargetId);
                    dto.ContentText = c?.Content ?? "[usunięto]";
                    dto.ContentAuthorName = c?.User?.Username ?? "?";
                }
                else if (r.Type == ReportType.Review)
                {
                    var rv = await _context.Reviews.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == r.TargetId);
                    dto.ContentText = rv?.Content ?? "[usunięto]";
                    dto.ContentAuthorName = rv?.User?.Username ?? "?";
                }

                dtos.Add(dto);
            }

            return View(dtos);
        }

        [HttpPost]
        public async Task<IActionResult> HandleReport(int reportId, string actionType, string? deleteReason)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null) return NotFound();

            if (actionType == "delete")
            {
                int? authorId   = null;
                string label    = "Twoja treść";

                if (report.Type == ReportType.Comment)
                {
                    var c = await _context.Comments.FindAsync(report.TargetId);
                    if (c != null)
                    {
                        authorId = c.UserId; label = "Twój komentarz";
                        var replies = await _context.Comments.Where(x => x.ParentCommentId == c.Id).ToListAsync();
                        _context.Comments.RemoveRange(replies);
                        _context.Comments.Remove(c);
                    }
                }
                else if (report.Type == ReportType.Article)
                {
                    var a = await _context.Articles.FindAsync(report.TargetId);
                    if (a != null) { authorId = a.AuthorId; label = "Twój artykuł"; _context.Articles.Remove(a); }
                }
                else if (report.Type == ReportType.Review)
                {
                    var rv = await _context.Reviews.Include(x => x.Game).FirstOrDefaultAsync(x => x.Id == report.TargetId);
                    if (rv != null)
                    {
                        authorId = rv.UserId;
                        label    = $"Twoja recenzja gry {rv.Game?.Title}";
                        _context.Reviews.Remove(rv);
                    }
                }

                if (authorId.HasValue)
                {
                    _notifications.NotifyUser(
                        authorId.Value,
                        NotificationType.ContentRemoved,
                        $"{label} została usunięta przez moderację w wyniku zgłoszenia.",
                        reason: string.IsNullOrWhiteSpace(deleteReason) ? "Naruszenie regulaminu." : deleteReason
                    );
                }

                TempData["SuccessMessage"] = "Treść usunięta i zgłoszenie zamknięte.";
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
            var appeal = await _context.Appeals.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == appealId);
            if (appeal == null) return NotFound();

            if (actionType == "approve")
            {
                appeal.Status = AppealStatus.Approved;
                if (appeal.User?.IsBanned == true) appeal.User.IsBanned = false;
                TempData["SuccessMessage"] = $"Odwołanie {appeal.User?.Username} zaakceptowane.";
            }
            else
            {
                appeal.Status = AppealStatus.Rejected;
                TempData["SuccessMessage"] = $"Odwołanie {appeal.User?.Username} odrzucone.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Appeals));
        }
    }
}
