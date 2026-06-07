using GameWiki.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly FavoriteService _favorites;

        public FavoritesController(FavoriteService favorites)
        {
            _favorites = favorites;
        }

        [HttpPost]
        public async Task<IActionResult> CreateList(string name, string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Redirect(returnUrl ?? "/");

            var userId = GetUserId();
            var ok = await _favorites.CreateListAsync(userId, name);

            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
                ? $"Utworzono nową kolekcję: {name}"
                : $"Masz już kolekcję o nazwie '{name}'.";

            return Redirect(returnUrl ?? "/Account/Profile");
        }

        [HttpPost]
        public async Task<IActionResult> AddToFavorite(int gameId, int listId, string returnUrl)
        {
            var ok = await _favorites.AddToFavoriteAsync(GetUserId(), gameId, listId);

            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
                ? "Gra dodana do kolekcji!"
                : "Ta gra znajduje się już na tej liście.";

            return Redirect(returnUrl ?? $"/Games/Details/{gameId}");
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromFavorite(int gameId, int listId, string returnUrl)
        {
            await _favorites.RemoveFromFavoriteAsync(GetUserId(), gameId, listId);
            TempData["SuccessMessage"] = "Gra została usunięta z kolekcji.";
            return Redirect(returnUrl ?? "/Account/Profile");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteList(int listId, string returnUrl)
        {
            var name = await _favorites.DeleteListAsync(GetUserId(), listId);
            if (name != null)
                TempData["SuccessMessage"] = $"Kolekcja '{name}' została usunięta.";
            return Redirect(returnUrl ?? "/Account/Profile");
        }

        private int GetUserId()
            => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }
}
