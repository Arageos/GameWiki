using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Services
{
    public class FavoriteService
    {
        private readonly GameWikiDbContext _context;

        public FavoriteService(GameWikiDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CreateListAsync(int userId, string name)
        {
            var exists = await _context.FavoriteLists
                .AnyAsync(l => l.UserId == userId && l.Name == name);

            if (exists) return false;

            _context.FavoriteLists.Add(new FavoriteList { UserId = userId, Name = name });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AddToFavoriteAsync(int userId, int gameId, int listId)
        {
            var list = await _context.FavoriteLists
                .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);

            if (list == null) return false;

            var exists = await _context.FavoriteGames
                .AnyAsync(fg => fg.FavoriteListId == listId && fg.GameId == gameId);

            if (exists) return false;

            _context.FavoriteGames.Add(new FavoriteGame { FavoriteListId = listId, GameId = gameId });
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> RemoveFromFavoriteAsync(int userId, int gameId, int listId)
        {
            var favoriteGame = await _context.FavoriteGames
                .Include(fg => fg.FavoriteList)
                .FirstOrDefaultAsync(fg =>
                    fg.GameId == gameId
                    && fg.FavoriteListId == listId
                    && fg.FavoriteList.UserId == userId);

            if (favoriteGame == null) return false;

            _context.FavoriteGames.Remove(favoriteGame);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string?> DeleteListAsync(int userId, int listId)
        {
            var list = await _context.FavoriteLists
                .FirstOrDefaultAsync(l => l.Id == listId && l.UserId == userId);

            if (list == null) return null;

            _context.FavoriteLists.Remove(list);
            await _context.SaveChangesAsync();
            return list.Name;
        }
    }
}
