using GameWiki.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly GameWikiDbContext _context;

        public FavoritesController(GameWikiDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateList(string name, string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(name)) return Redirect(returnUrl ?? "/");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var exists = await _context.FavoriteLists.AnyAsync(l => l.UserId == userId && l.Name == name);
            if (exists)
            {
                TempData["ErrorMessage"] = $"Masz już kolekcję o nazwie '{name}'.";
                return Redirect(returnUrl ?? "/Account/Profile");
            }
            var newList = new FavoriteList
            {
                UserId = userId,
                Name = name
            };

            _context.FavoriteLists.Add(newList);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Utworzono nową kolekcję: {name}";
            return Redirect(returnUrl ?? "/Account/Profile");
        }

        [HttpPost]
        public async Task<IActionResult> AddToFavorite(int gameId, int listId, string returnUrl)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var list = await _context.FavoriteLists.FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);
            if (list == null) return Unauthorized();

            var exists = await _context.FavoriteGames.AnyAsync(fg => fg.FavoriteListId == listId && fg.GameId == gameId);
            if (!exists)
            {
                _context.FavoriteGames.Add(new FavoriteGame { FavoriteListId = listId, GameId = gameId });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Gra dodana do kolekcji!";
            }
            else
            {
                TempData["ErrorMessage"] = "Ta gra znajduje się już na tej liście.";
            }

            return Redirect(returnUrl ?? $"/Games/Details/{gameId}");
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromFavorite(int gameId, int listId, string returnUrl)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var favoriteGame = await _context.FavoriteGames
                .Include(fg => fg.FavoriteList)
                .FirstOrDefaultAsync(fg => fg.GameId == gameId && fg.FavoriteListId == listId && fg.FavoriteList.UserId == userId);

            if (favoriteGame != null)
            {
                _context.FavoriteGames.Remove(favoriteGame);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Gra została usunięta z kolekcji.";
            }

            return Redirect(returnUrl ?? "/Account/Profile");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteList(int listId, string returnUrl)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var list = await _context.FavoriteLists
                .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);

            if (list != null)
            {
                _context.FavoriteLists.Remove(list);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Kolekcja '{list.Name}' została usunięta.";
            }

            return Redirect(returnUrl ?? "/Account/Profile");
        }
    }
}