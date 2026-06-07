using GameWiki.DTOs.Review;
using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Services
{
    public class ReviewService
    {
        private readonly GameWikiDbContext _context;
        private readonly NotificationService _notifications;

        public ReviewService(GameWikiDbContext context, NotificationService notifications)
        {
            _context       = context;
            _notifications = notifications;
        }

        public async Task<List<ReviewDto>> GetReviewsAsync(int gameId, int? currentUserId)
        {
            var game = await _context.Games.FindAsync(gameId);
            if (game == null) return new List<ReviewDto>();

            return await _context.Reviews
                .Where(r => r.GameId == gameId)
                .Include(r => r.User)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReviewDto
                {
                    Id             = r.Id,
                    GameId         = r.GameId,
                    UserId         = r.UserId,
                    GameTitle      = game.Title,
                    Username       = r.User.Username,
                    AvatarUrl      = r.User.ProfilePictureUrl,
                    AuthorRoleName = r.User.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault(),
                    Rating         = r.Rating,
                    Content        = r.Content,
                    CreatedAt      = r.CreatedAt,
                    IsOwner        = currentUserId != null && r.UserId == currentUserId,
                    IsVerified     = r.IsVerified
                })
                .ToListAsync();
        }

        public async Task<Review?> GetReviewForEditAsync(int reviewId, int userId, bool isMod)
        {
            return await _context.Reviews
                .Include(r => r.Game)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == reviewId && (r.UserId == userId || isMod));
        }

        public async Task<Review?> GetExistingReviewAsync(int gameId, int userId)
        {
            return await _context.Reviews
                .FirstOrDefaultAsync(r => r.GameId == gameId && r.UserId == userId);
        }

        public async Task<Review> CreateAsync(int gameId, int userId, CreateReviewDto dto, bool isMod)
        {
            bool hasContent = !string.IsNullOrWhiteSpace(dto.Content);
            bool isVerified = isMod || !hasContent;

            var review = new Review
            {
                GameId     = gameId,
                UserId     = userId,
                Rating     = dto.Rating,
                Content    = dto.Content,
                IsVerified = isVerified,
                CreatedAt  = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            if (!isVerified && hasContent)
            {
                var author = await _context.Users.FindAsync(userId);
                var game   = await _context.Games.FindAsync(gameId);
                await _notifications.NotifyModsAsync(
                    NotificationType.NewReview,
                    $"Nowa recenzja do weryfikacji — gra: {game?.Title}, autor: {author?.Username}",
                    "/Admin/PendingReviews"
                );
                await _context.SaveChangesAsync();
            }

            return review;
        }

        public async Task UpdateAsync(Review review, EditReviewDto dto, int editorId)
        {
            bool isModEdit = review.UserId != editorId;
            review.Rating  = dto.Rating;
            review.Content = dto.Content;

            if (isModEdit)
            {
                _notifications.NotifyUser(
                    review.UserId,
                    NotificationType.ContentEdited,
                    $"Twoja recenzja gry {review.Game?.Title} została zedytowana przez moderację.",
                    actionUrl: $"/Reviews/Index?gameId={review.GameId}"
                );
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Review review)
        {
            await ResolveRelatedReportsAsync(review.Id);
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteByModAsync(Review review, string? reason)
        {
            _notifications.NotifyUser(
                review.UserId,
                NotificationType.ContentRemoved,
                $"Twoja recenzja gry {review.Game?.Title} została usunięta przez moderację.",
                reason: string.IsNullOrWhiteSpace(reason) ? "Naruszenie regulaminu." : reason
            );

            await ResolveRelatedReportsAsync(review.Id);
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
        }

        public async Task<Review?> VerifyAsync(int reviewId)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            if (review == null) return null;

            review.IsVerified = true;

            _notifications.NotifyUser(
                review.UserId,
                NotificationType.ContentEdited,
                "Twoja recenzja została zweryfikowana i jest teraz widoczna publicznie.",
                actionUrl: $"/Reviews/Index?gameId={review.GameId}"
            );

            await _context.SaveChangesAsync();
            return review;
        }

        public async Task<List<Review>> GetPendingAsync()
        {
            return await _context.Reviews
                .Where(r => !r.IsVerified && !string.IsNullOrEmpty(r.Content))
                .Include(r => r.User)
                .Include(r => r.Game)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<string?> GetUserRoleAsync(int userId)
        {
            return await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync();
        }

        private async Task ResolveRelatedReportsAsync(int reviewId)
        {
            var reports = await _context.Reports
                .Where(r => r.Type == ReportType.Review && r.TargetId == reviewId
                         && r.Status == ReportStatus.Pending)
                .ToListAsync();
            foreach (var r in reports)
                r.Status = ReportStatus.Resolved;
        }
    }
}
