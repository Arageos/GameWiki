using GameWiki.DTOs.Article;
using GameWiki.Models;
using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    public class ArticleController : Controller
    {
        private readonly ArticleService _articleService;

        public ArticleController(ArticleService articleService)
        {
            _articleService = articleService;
        }

        public async Task<IActionResult> Index(int? gameId, string? gameSearch)
        {
            var (articles, selectedGameTitle) = await _articleService.GetArticlesAsync(gameId, gameSearch);

            ViewBag.SelectedGameId = gameId;
            ViewBag.GameSearch = gameSearch;
            ViewBag.SelectedGameTitle = selectedGameTitle;

            return View(articles);
        }

        [HttpGet]
        public async Task<IActionResult> SearchGames(string q)
        {
            var results = await _articleService.SearchGamesAsync(q);
            return Json(results);
        }

        public async Task<IActionResult> Details(int id)
        {
            var article = await _articleService.GetArticleWithDetailsAsync(id);
            if (article == null) return NotFound();

            var allComments = await _articleService.GetArticleCommentsAsync(id);
            ViewBag.AllComments = allComments;

            var currentUserId = GetCurrentUserId();
            ViewBag.MyReactions = currentUserId.HasValue
                ? await _articleService.GetUserReactionsAsync(currentUserId.Value, allComments.Select(c => c.Id).ToList())
                : new List<CommentReaction>();

            return View(article);
        }

        [Authorize]
        public IActionResult Create(int? gameId)
        {
            return View(new CreateArticleDto { GameId = gameId ?? 0 });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateArticleDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            bool isMod = User.IsInRole("Admin") || User.IsInRole("Moderator");

            await _articleService.CreateArticleAsync(dto, userId.Value, isMod);

            TempData["SuccessMessage"] = isMod
                ? "Artykuł został opublikowany."
                : "Artykuł został przesłany i czeka na weryfikację przez moderatora.";

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var article = await _articleService.GetArticleForEditAsync(id);
            if (article == null) return NotFound();

            if (!CanModify(article.AuthorId)) return Forbid();

            return View(article);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditArticleDto dto)
        {
            var article = await _articleService.GetArticleForEditAsync(id);
            if (article == null) return NotFound();

            if (!CanModify(article.AuthorId)) return Forbid();

            var editorId = GetCurrentUserId()!.Value;

            await _articleService.UpdateArticleWithNotificationAsync(article, dto, editorId);

            TempData["SuccessMessage"] = "Artykuł został zaktualizowany.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? deleteReason)
        {
            var article = await _articleService.GetArticleForEditAsync(id);
            if (article == null) return NotFound();

            if (!CanModify(article.AuthorId)) return Forbid();

            await _articleService.DeleteArticleAsync(article, GetCurrentUserId(), deleteReason);

            TempData["SuccessMessage"] = "Artykuł został usunięty.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            var article = await _articleService.VerifyArticleAsync(id);
            if (article == null) return NotFound();

            TempData["SuccessMessage"] = "Artykuł został zatwierdzony.";
            return RedirectToAction("PendingArticles", "Admin");
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? deleteReason)
        {
            var article = await _articleService.RejectArticleAsync(id, deleteReason);
            if (article == null) return NotFound();

            TempData["SuccessMessage"] = "Artykuł został odrzucony i usunięty.";
            return RedirectToAction("PendingArticles", "Admin");
        }

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

            await _articleService.AddCommentAsync(articleId, userId.Value, content, parentCommentId);
            return RedirectToAction(nameof(Details), new { id = articleId });
        }

        [Authorize]
        public async Task<IActionResult> EditComment(int commentId)
        {
            var comment = await _articleService.GetCommentAsync(commentId);
            if (comment == null) return NotFound();

            if (!CanModify(comment.UserId)) return Forbid();

            ViewBag.ArticleId = comment.ArticleId;
            ViewBag.IsModEdit = (User.IsInRole("Admin") || User.IsInRole("Moderator"))
                                && comment.UserId != GetCurrentUserId();
            ViewBag.OriginalAuthor = comment.User?.Username;
            return View(comment);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(int commentId, int articleId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Komentarz nie może być pusty.";
                return RedirectToAction(nameof(Details), new { id = articleId });
            }

            var comment = await _articleService.GetCommentAsync(commentId);
            if (comment == null) return NotFound();

            if (!CanModify(comment.UserId)) return Forbid();

            var editorId = GetCurrentUserId()!.Value;
            await _articleService.UpdateCommentAsync(comment, content, editorId);

            TempData["SuccessMessage"] = "Komentarz został zaktualizowany.";
            return RedirectToAction(nameof(Details), new { id = articleId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId, int articleId, string? deleteReason)
        {
            var comment = await _articleService.GetCommentAsync(commentId);
            if (comment == null) return NotFound();

            if (!CanModify(comment.UserId)) return Forbid();

            await _articleService.DeleteCommentAsync(comment, GetCurrentUserId(), deleteReason);
            return RedirectToAction(nameof(Details), new { id = articleId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> React(int commentId, int articleId, ReactionType type)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            await _articleService.ReactAsync(commentId, userId.Value, type);
            return RedirectToAction(nameof(Details), new { id = articleId });
        }

        private int? GetCurrentUserId()
        {
            var val = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(val, out int id) ? id : null;
        }

        private bool CanModify(int ownerId)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Moderator")) return true;
            return GetCurrentUserId() == ownerId;
        }
    }
}
