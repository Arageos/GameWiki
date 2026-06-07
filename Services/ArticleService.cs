using GameWiki.DTOs.Article;
using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Services
{
    public class ArticleService
    {
        private readonly GameWikiDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly NotificationService _notifications;

        public ArticleService(GameWikiDbContext context, IWebHostEnvironment env,
                              NotificationService notifications)
        {
            _context       = context;
            _env           = env;
            _notifications = notifications;
        }

        public Task NotifyModsAsync(NotificationType type, string message, string? actionUrl = null)
            => _notifications.NotifyModsAsync(type, message, actionUrl);

        public async Task<(List<Article> Articles, string? SelectedGameTitle)>
            GetArticlesAsync(int? gameId, string? gameSearch)
        {
            var query = _context.Articles
                .Include(a => a.Author)
                .Include(a => a.Game)
                .AsQueryable();

            string? selectedGameTitle = null;

            if (gameId.HasValue)
            {
                query = query.Where(a => a.GameId == gameId.Value);
                selectedGameTitle = await _context.Games
                    .Where(g => g.Id == gameId.Value)
                    .Select(g => g.Title)
                    .FirstOrDefaultAsync();
            }
            else if (!string.IsNullOrWhiteSpace(gameSearch))
            {
                query = query.Where(a => a.Game.Title.Contains(gameSearch));
            }

            return (await query.OrderByDescending(a => a.CreatedAt).ToListAsync(), selectedGameTitle);
        }

        public async Task<Article?> GetArticleWithDetailsAsync(int id)
        {
            return await _context.Articles
                .Include(a => a.Game)
                .Include(a => a.Author)
                    .ThenInclude(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                .Include(a => a.Blocks.OrderBy(b => b.Order))
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<List<Comment>> GetArticleCommentsAsync(int articleId)
        {
            return await _context.Comments
                .Where(c => c.ArticleId == articleId)
                .Include(c => c.User)
                .Include(c => c.Reactions)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<CommentReaction>> GetUserReactionsAsync(int userId, List<int> commentIds)
        {
            return await _context.CommentReactions
                .Where(r => r.UserId == userId && commentIds.Contains(r.CommentId))
                .ToListAsync();
        }

        public async Task<Article> CreateArticleAsync(CreateArticleDto dto, int authorId, bool isVerified)
        {
            var article = new Article
            {
                GameId     = dto.GameId,
                AuthorId   = authorId,
                Title      = dto.Title,
                IsVerified = isVerified,
                CreatedAt  = DateTime.UtcNow
            };

            if (dto.CoverImage != null && dto.CoverImage.Length > 0)
                article.CoverImageUrl = await SaveImageAsync(dto.CoverImage);

            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            await SaveBlocksAsync(article.Id, dto.Blocks);
            await _context.SaveChangesAsync();

            if (!isVerified)
            {
                var author = await _context.Users.FindAsync(authorId);
                await _notifications.NotifyModsAsync(
                    NotificationType.NewArticle,
                    $"Nowy artykuł do weryfikacji: {article.Title} — autor: {author?.Username}",
                    "/Admin/PendingArticles"
                );
                await _context.SaveChangesAsync();
            }

            return article;
        }

        public async Task<Article?> GetArticleForEditAsync(int id)
        {
            return await _context.Articles
                .Include(a => a.Blocks.OrderBy(b => b.Order))
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task UpdateArticleAsync(Article article, EditArticleDto dto)
        {
            article.Title     = dto.Title;
            article.UpdatedAt = DateTime.UtcNow;

            if (dto.CoverImage != null && dto.CoverImage.Length > 0)
                article.CoverImageUrl = await SaveImageAsync(dto.CoverImage);

            _context.ArticleBlocks.RemoveRange(article.Blocks);
            await SaveBlocksAsync(article.Id, dto.Blocks);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateArticleWithNotificationAsync(Article article, EditArticleDto dto, int editorId)
        {
            await UpdateArticleAsync(article, dto);

            if (article.AuthorId != editorId)
            {
                _notifications.NotifyUser(
                    article.AuthorId,
                    NotificationType.ContentEdited,
                    $"Twój artykuł {article.Title} został zedytowany przez moderację.",
                    actionUrl: $"/Article/Details/{article.Id}"
                );
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteArticleAsync(Article article, int? moderatorId, string? reason)
        {
            if (moderatorId.HasValue && article.AuthorId != moderatorId.Value)
            {
                _notifications.NotifyUser(
                    article.AuthorId,
                    NotificationType.ContentRemoved,
                    "Twój artykuł został usunięty przez moderację.",
                    reason: string.IsNullOrWhiteSpace(reason) ? "Naruszenie regulaminu." : reason
                );
            }

            await ResolveRelatedReportsAsync(ReportType.Article, article.Id);
            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();
        }

        public async Task<Article?> VerifyArticleAsync(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return null;
            article.IsVerified = true;
            await _context.SaveChangesAsync();
            return article;
        }

        public async Task<Article?> RejectArticleAsync(int id, string? reason)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return null;

            _notifications.NotifyUser(
                article.AuthorId,
                NotificationType.ContentRemoved,
                "Twój artykuł został odrzucony przez moderację.",
                reason: string.IsNullOrWhiteSpace(reason) ? "Artykuł nie spełnia wymogów serwisu." : reason
            );

            await ResolveRelatedReportsAsync(ReportType.Article, id);
            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();
            return article;
        }

        public async Task AddCommentAsync(int articleId, int userId, string content, int? parentCommentId)
        {
            _context.Comments.Add(new Comment
            {
                ArticleId       = articleId,
                UserId          = userId,
                Content         = content.Trim(),
                CreatedAt       = DateTime.UtcNow,
                ParentCommentId = parentCommentId
            });
            await _context.SaveChangesAsync();
        }

        public async Task<Comment?> GetCommentAsync(int commentId)
        {
            return await _context.Comments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == commentId);
        }

        public async Task UpdateCommentAsync(Comment comment, string newContent, int editorId)
        {
            bool isModEdit  = comment.UserId != editorId;
            comment.Content  = newContent.Trim();
            comment.UpdatedAt = DateTime.UtcNow;

            if (isModEdit)
            {
                _notifications.NotifyUser(
                    comment.UserId,
                    NotificationType.ContentEdited,
                    "Twój komentarz został zedytowany przez moderację."
                );
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteCommentAsync(Comment comment, int? moderatorId, string? reason)
        {
            if (moderatorId.HasValue && comment.UserId != moderatorId.Value)
            {
                _notifications.NotifyUser(
                    comment.UserId,
                    NotificationType.ContentRemoved,
                    "Twój komentarz został usunięty przez moderację.",
                    reason: string.IsNullOrWhiteSpace(reason) ? "Naruszenie regulaminu." : reason
                );
            }

            await ResolveRelatedReportsAsync(ReportType.Comment, comment.Id);
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
        }

        public async Task ReactAsync(int commentId, int userId, ReactionType type)
        {
            var existing = await _context.CommentReactions
                .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId);

            if (existing == null)
                _context.CommentReactions.Add(new CommentReaction
                    { CommentId = commentId, UserId = userId, Type = type });
            else if (existing.Type == type)
                _context.CommentReactions.Remove(existing);
            else
                existing.Type = type;

            await _context.SaveChangesAsync();
        }

        public async Task<List<object>> SearchGamesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new();

            return await _context.Games
                .Where(g => g.Title.Contains(query))
                .OrderBy(g => g.Title)
                .Take(8)
                .Select(g => (object)new { g.Id, g.Title })
                .ToListAsync();
        }

        private async Task SaveBlocksAsync(int articleId, List<BlockInputDto>? blocks)
        {
            if (blocks == null) return;
            int order = 0;

            foreach (var blockDto in blocks)
            {
                if (blockDto.Type == ArticleBlockType.Text && !string.IsNullOrWhiteSpace(blockDto.TextContent))
                {
                    _context.ArticleBlocks.Add(new ArticleBlock
                    {
                        ArticleId = articleId, Type = ArticleBlockType.Text,
                        Content   = blockDto.TextContent, Order = order++
                    });
                }
                else if (blockDto.Type == ArticleBlockType.Image)
                {
                    string? url = null;
                    if (blockDto.ImageFile != null && blockDto.ImageFile.Length > 0)
                        url = await SaveImageAsync(blockDto.ImageFile);
                    else if (!string.IsNullOrWhiteSpace(blockDto.ExistingImageUrl))
                        url = blockDto.ExistingImageUrl;

                    if (url != null)
                        _context.ArticleBlocks.Add(new ArticleBlock
                        {
                            ArticleId = articleId, Type = ArticleBlockType.Image,
                            Content   = url, Order = order++
                        });
                }
            }
        }

        private async Task ResolveRelatedReportsAsync(ReportType type, int targetId)
        {
            var reports = await _context.Reports
                .Where(r => r.Type == type && r.TargetId == targetId && r.Status == ReportStatus.Pending)
                .ToListAsync();
            foreach (var r in reports) r.Status = ReportStatus.Resolved;
        }

        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads", "articles");
            Directory.CreateDirectory(uploads);
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploads, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/uploads/articles/{fileName}";
        }
    }
}