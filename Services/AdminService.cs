using GameWiki.DTOs.Admin;
using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Services
{
    public class AdminService
    {
        private readonly GameWikiDbContext _context;
        private readonly NotificationService _notifications;
        private readonly ReviewService _reviews;

        public AdminService(GameWikiDbContext context, NotificationService notifications, ReviewService reviews)
        {
            _context = context;
            _notifications = notifications;
            _reviews = reviews;
        }

        public async Task<List<UserListDto>> GetUsersAsync()
        {
            return await _context.Users
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
                .OrderBy(u => u.RoleName).ThenBy(u => u.Username)
                .ToListAsync();
        }

        public async Task<(bool Success, string Message, string? Username)> ToggleBanAsync(
            int targetUserId, int currentUserId, string currentUserRole, string? banReason)
        {
            if (targetUserId == currentUserId)
                return (false, "Nie możesz zbanować samego siebie!", null);

            var targetUser = await _context.Users.FindAsync(targetUserId);
            if (targetUser == null) return (false, "Nie znaleziono użytkownika.", null);

            var targetRole = await _context.UserRoles
                .Where(ur => ur.UserId == targetUserId)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync() ?? "User";

            if (currentUserRole == "Moderator" && (targetRole == "Admin" || targetRole == "Moderator"))
                return (false, "Moderator może banować tylko zwykłych użytkowników.", null);

            targetUser.IsBanned = !targetUser.IsBanned;

            _notifications.NotifyUser(
                targetUserId,
                targetUser.IsBanned ? NotificationType.Ban : NotificationType.Unban,
                targetUser.IsBanned
                    ? "Twoje konto zostało zablokowane przez administrację."
                    : "Twoje konto zostało odblokowane przez administrację.",
                reason: targetUser.IsBanned ? banReason : null
            );

            await _context.SaveChangesAsync();

            var message = targetUser.IsBanned
                ? $"Użytkownik {targetUser.Username} został zbanowany."
                : $"Użytkownik {targetUser.Username} został odbanowany.";

            return (true, message, targetUser.Username);
        }

        public async Task<(bool Success, string Message)> ToggleModRoleAsync(int targetUserId)
        {
            var targetUser = await _context.Users.FindAsync(targetUserId);
            if (targetUser == null) return (false, "Nie znaleziono użytkownika.");

            var userRoleObj = await _context.UserRoles
                .Include(ur => ur.Role)
                .FirstOrDefaultAsync(ur => ur.UserId == targetUserId);

            var currentRole = userRoleObj?.Role.Name ?? "User";

            if (currentRole == "Admin")
                return (false, "Nie można zmienić uprawnień Administratora.");

            var modRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Moderator");
            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");

            if (userRoleObj != null) _context.UserRoles.Remove(userRoleObj);

            string message;
            if (currentRole == "User")
            {
                _context.UserRoles.Add(new UserRole { UserId = targetUserId, RoleId = modRole!.Id });
                message = $"{targetUser.Username} awansował na Moderatora.";
            }
            else
            {
                _context.UserRoles.Add(new UserRole { UserId = targetUserId, RoleId = userRole!.Id });
                message = $"{targetUser.Username} zdegradowany do Użytkownika.";
            }

            await _context.SaveChangesAsync();
            return (true, message);
        }

        public async Task<List<Article>> GetPendingArticlesAsync()
        {
            return await _context.Articles
                .Where(a => !a.IsVerified)
                .Include(a => a.Author)
                .Include(a => a.Game)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Review>> GetPendingReviewsAsync()
            => await _reviews.GetPendingAsync();

        public async Task VerifyReviewAsync(int reviewId)
            => await _reviews.VerifyAsync(reviewId);

        public async Task<Review?> GetReviewWithGameAsync(int reviewId)
        {
            return await _context.Reviews
                .Include(r => r.Game)
                .FirstOrDefaultAsync(r => r.Id == reviewId);
        }

        public async Task DeleteReviewByModAsync(Review review, string? reason)
            => await _reviews.DeleteByModAsync(review, reason);

        public async Task<List<ReportItemDto>> GetReportsAsync()
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
                    ReportType.Review => await _context.Reviews.AnyAsync(rv => rv.Id == r.TargetId),
                    _ => true
                };
                if (!exists) { r.Status = ReportStatus.Resolved; reports.Remove(r); anyResolved = true; }
            }

            if (anyResolved) await _context.SaveChangesAsync();

            var dtos = new List<ReportItemDto>();
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

            return dtos;
        }

        public async Task<(bool Found, string Message)> HandleReportAsync(
            int reportId, string actionType, string? deleteReason)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null) return (false, "");

            if (actionType == "delete")
            {
                int? authorId = null;
                string label = "Twoja treść";

                if (report.Type == ReportType.Comment)
                {
                    var c = await _context.Comments.FindAsync(report.TargetId);
                    if (c != null)
                    {
                        authorId = c.UserId;
                        label = "Twój komentarz";
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
                        label = $"Twoja recenzja gry {rv.Game?.Title}";
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

                report.Status = ReportStatus.Resolved;
                await _context.SaveChangesAsync();
                return (true, "Treść usunięta i zgłoszenie zamknięte.");
            }

            report.Status = ReportStatus.Resolved;
            await _context.SaveChangesAsync();
            return (true, "Zgłoszenie zostało odrzucone.");
        }

        public async Task<List<Appeal>> GetAppealsAsync()
        {
            return await _context.Appeals
                .Include(a => a.User)
                .Where(a => a.Status == AppealStatus.Pending)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<(bool Found, string Message)> HandleAppealAsync(int appealId, string actionType)
        {
            var appeal = await _context.Appeals
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == appealId);

            if (appeal == null) return (false, "");

            if (actionType == "approve")
            {
                appeal.Status = AppealStatus.Approved;
                if (appeal.User?.IsBanned == true) appeal.User.IsBanned = false;
                await _context.SaveChangesAsync();
                return (true, $"Odwołanie {appeal.User?.Username} zaakceptowane.");
            }

            appeal.Status = AppealStatus.Rejected;
            await _context.SaveChangesAsync();
            return (true, $"Odwołanie {appeal.User?.Username} odrzucone.");
        }
    }
}