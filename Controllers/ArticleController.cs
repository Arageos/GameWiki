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

        // ── LISTA ARTYKUŁÓW ──────────────────────────────────────────────

        public async Task<IActionResult> Index(int? gameId, string? gameSearch)
        {
            var (articles, selectedGameTitle) = await _articleService.GetArticlesAsync(gameId, gameSearch);

            ViewBag.SelectedGameId = gameId;
            ViewBag.GameSearch = gameSearch;
            ViewBag.SelectedGameTitle = selectedGameTitle;

            return View(articles);
        }

        // ── AUTOCOMPLETE ─────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> SearchGames(string q)
        {
            var results = await _articleService.SearchGamesAsync(q);
            return Json(results);
        }

        // ── SZCZEGÓŁY ────────────────────────────────────────────────────

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

        // ── TWORZENIE ────────────────────────────────────────────────────

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

        // ── EDYCJA ───────────────────────────────────────────────────────

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

            await _articleService.UpdateArticleAsync(article, dto);

            TempData["SuccessMessage"] = "Artykuł został zaktualizowany.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── USUWANIE ─────────────────────────────────────────────────────

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

        // ── MODERACJA ────────────────────────────────────────────────────

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

        // ── KOMENTARZE ───────────────────────────────────────────────────

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

        // ── REAKCJE ──────────────────────────────────────────────────────

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

        // ── PRYWATNE HELPERY ─────────────────────────────────────────────

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