using GameWiki.Models;
using GameWiki.DTOs.Article;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    public class ArticleController : Controller
    {
        private readonly GameWikiDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ArticleController(GameWikiDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ─── GLOBALNA LISTA ARTYKUŁÓW (opcjonalny filtr po grze) ──────────────────

        public async Task<IActionResult> Index(int? gameId)
        {
            ViewBag.Games = await _context.Games.OrderBy(g => g.Title).ToListAsync();
            ViewBag.SelectedGameId = gameId;

            var query = _context.Articles
                .Include(a => a.Author)
                .Include(a => a.Game)
                .AsQueryable();

            if (gameId.HasValue)
                query = query.Where(a => a.GameId == gameId.Value);

            var articles = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

            return View(articles);
        }

        // ─── SZCZEGÓŁY ARTYKUŁU ────────────────────────────────────────────────────

        public async Task<IActionResult> Details(int id)
        {
            var article = await _context.Articles
                .Include(a => a.Game)
                .Include(a => a.Author)
                .Include(a => a.Blocks.OrderBy(b => b.Order))
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article == null) return NotFound();

            var allComments = await _context.Comments
                .Where(c => c.ArticleId == id)
                .Include(c => c.User)
                .Include(c => c.Reactions)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            ViewBag.AllComments = allComments;

            var currentUserId = GetCurrentUserId();
            if (currentUserId.HasValue)
            {
                ViewBag.MyReactions = await _context.CommentReactions
                    .Where(r => r.UserId == currentUserId.Value &&
                                allComments.Select(c => c.Id).Contains(r.CommentId))
                    .ToListAsync();
            }
            else
            {
                ViewBag.MyReactions = new List<CommentReaction>();
            }

            return View(article);
        }

        // ─── TWORZENIE ARTYKUŁU ────────────────────────────────────────────────────

        [Authorize]
        public async Task<IActionResult> Create(int? gameId)
        {
            ViewBag.Games = await _context.Games.OrderBy(g => g.Title).ToListAsync();
            return View(new CreateArticleDto { GameId = gameId ?? 0 });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateArticleDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Games = await _context.Games.OrderBy(g => g.Title).ToListAsync();
                return View(dto);
            }

            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");

            var article = new Article
            {
                GameId = dto.GameId,
                AuthorId = userId.Value,
                Title = dto.Title,
                IsVerified = isMod,
                CreatedAt = DateTime.UtcNow
            };

            if (dto.CoverImage != null && dto.CoverImage.Length > 0)
                article.CoverImageUrl = await SaveImageAsync(dto.CoverImage);

            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            if (dto.Blocks != null)
            {
                int order = 0;
                foreach (var blockDto in dto.Blocks)
                {
                    if (blockDto.Type == ArticleBlockType.Text && !string.IsNullOrWhiteSpace(blockDto.TextContent))
                    {
                        _context.ArticleBlocks.Add(new ArticleBlock
                        {
                            ArticleId = article.Id,
                            Type = ArticleBlockType.Text,
                            Content = blockDto.TextContent,
                            Order = order++
                        });
                    }
                    else if (blockDto.Type == ArticleBlockType.Image && blockDto.ImageFile != null && blockDto.ImageFile.Length > 0)
                    {
                        var url = await SaveImageAsync(blockDto.ImageFile);
                        _context.ArticleBlocks.Add(new ArticleBlock
                        {
                            ArticleId = article.Id,
                            Type = ArticleBlockType.Image,
                            Content = url,
                            Order = order++
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = isMod
                ? "Artykuł został opublikowany."
                : "Artykuł został przesłany i czeka na weryfikację przez moderatora.";

            return RedirectToAction(nameof(Index));
        }

        // ─── EDYCJA ARTYKUŁU ───────────────────────────────────────────────────────

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var article = await _context.Articles
                .Include(a => a.Blocks.OrderBy(b => b.Order))
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article == null) return NotFound();

            var userId = GetCurrentUserId();
            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");
            if (!isMod && article.AuthorId != userId) return Forbid();

            ViewBag.Games = await _context.Games.OrderBy(g => g.Title).ToListAsync();
            return View(article);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditArticleDto dto)
        {
            var article = await _context.Articles
                .Include(a => a.Blocks)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (article == null) return NotFound();

            var userId = GetCurrentUserId();
            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");
            if (!isMod && article.AuthorId != userId) return Forbid();

            article.Title = dto.Title;
            article.UpdatedAt = DateTime.UtcNow;

            if (dto.CoverImage != null && dto.CoverImage.Length > 0)
                article.CoverImageUrl = await SaveImageAsync(dto.CoverImage);

            _context.ArticleBlocks.RemoveRange(article.Blocks);

            if (dto.Blocks != null)
            {
                int order = 0;
                foreach (var blockDto in dto.Blocks)
                {
                    if (blockDto.Type == ArticleBlockType.Text && !string.IsNullOrWhiteSpace(blockDto.TextContent))
                    {
                        _context.ArticleBlocks.Add(new ArticleBlock
                        {
                            ArticleId = article.Id,
                            Type = ArticleBlockType.Text,
                            Content = blockDto.TextContent,
                            Order = order++
                        });
                    }
                    else if (blockDto.Type == ArticleBlockType.Image && blockDto.ImageFile != null && blockDto.ImageFile.Length > 0)
                    {
                        var url = await SaveImageAsync(blockDto.ImageFile);
                        _context.ArticleBlocks.Add(new ArticleBlock
                        {
                            ArticleId = article.Id,
                            Type = ArticleBlockType.Image,
                            Content = url,
                            Order = order++
                        });
                    }
                    else if (blockDto.Type == ArticleBlockType.Image && !string.IsNullOrWhiteSpace(blockDto.ExistingImageUrl))
                    {
                        _context.ArticleBlocks.Add(new ArticleBlock
                        {
                            ArticleId = article.Id,
                            Type = ArticleBlockType.Image,
                            Content = blockDto.ExistingImageUrl,
                            Order = order++
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Artykuł został zaktualizowany.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ─── USUWANIE ARTYKUŁU ─────────────────────────────────────────────────────

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return NotFound();

            var userId = GetCurrentUserId();
            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");
            if (!isMod && article.AuthorId != userId) return Forbid();

            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Artykuł został usunięty.";
            return RedirectToAction(nameof(Index));
        }

        // ─── MODERACJA ─────────────────────────────────────────────────────────────

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return NotFound();

            article.IsVerified = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Artykuł został zatwierdzony.";
            return RedirectToAction("PendingArticles", "Admin");
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null) return NotFound();

            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Artykuł został odrzucony i usunięty.";
            return RedirectToAction("PendingArticles", "Admin");
        }

        // ─── KOMENTARZE ────────────────────────────────────────────────────────────

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int articleId, string content, int? parentCommentId)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Komentarz nie może być pusty.";
                return RedirectToAction(nameof(Details), new { id = articleId });
            }

            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            _context.Comments.Add(new Comment
            {
                ArticleId = articleId,
                UserId = userId.Value,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow,
                ParentCommentId = parentCommentId
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = articleId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId, int articleId)
        {
            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null) return NotFound();

            var userId = GetCurrentUserId();
            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");
            if (!isMod && comment.UserId != userId) return Forbid();

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = articleId });
        }

        // ─── REAKCJE ───────────────────────────────────────────────────────────────

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> React(int commentId, int articleId, ReactionType type)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var existing = await _context.CommentReactions
                .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == userId.Value);

            if (existing == null)
            {
                _context.CommentReactions.Add(new CommentReaction
                {
                    CommentId = commentId,
                    UserId = userId.Value,
                    Type = type
                });
            }
            else if (existing.Type == type)
            {
                _context.CommentReactions.Remove(existing);
            }
            else
            {
                existing.Type = type;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = articleId });
        }

        // ─── HELPERS ───────────────────────────────────────────────────────────────

        private int? GetCurrentUserId()
        {
            var val = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(val, out int id) ? id : null;
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