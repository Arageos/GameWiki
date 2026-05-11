using GameWiki.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GameWiki.Controllers
{
    [Authorize] // Tylko zalogowani mogą tu coś robić
    public class FavoritesController : Controller
    {
        private readonly GameWikiDbContext _context;

        public FavoritesController(GameWikiDbContext context)
        {
            _context = context;
        }

        // --- TWORZENIE NOWEJ LISTY ---
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

        // --- DODAWANIE GRY DO LISTY (przyda się za chwilę) ---
        [HttpPost]
        public async Task<IActionResult> AddToFavorite(int gameId, int listId, string returnUrl)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Zabezpieczenie: czy ta lista na pewno należy do tego usera?
            var list = await _context.FavoriteLists.FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);
            if (list == null) return Unauthorized();

            // Zabezpieczenie: czy gra już jest na tej liście?
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
        // --- USUWANIE GRY Z LISTY ---
        [HttpPost]
        public async Task<IActionResult> RemoveFromFavorite(int gameId, int listId, string returnUrl)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            // Szukamy konkretnego powiązania gry z listą, upewniając się, że lista należy do użytkownika
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

        // --- USUWANIE CAŁEJ LISTY ---
        [HttpPost]
        public async Task<IActionResult> DeleteList(int listId, string returnUrl)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            // Znajdujemy listę (sprawdzając właściciela)
            var list = await _context.FavoriteLists
                .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);

            if (list != null)
            {
                // Entity Framework automatycznie usunie powiązane gry (FavoriteGames) 
                // dzięki kaskadowemu usuwaniu zdefiniowanemu w bazie
                _context.FavoriteLists.Remove(list);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Kolekcja '{list.Name}' została usunięta.";
            }

            return Redirect(returnUrl ?? "/Account/Profile");
        }
    }
}