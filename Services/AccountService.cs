using GameWiki.DTOs.Auth;
using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Services
{
    public class AccountService
    {
        private readonly GameWikiDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AccountService(GameWikiDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env     = env;
        }

        public async Task<(User? User, string? Field, string? Error)> RegisterAsync(RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return (null, "Email", "Ten email jest już zajęty");

            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
                return (null, "Username", "Ta nazwa użytkownika jest już zajęta");

            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User")
                           ?? new Role { Name = "User" };

            if (userRole.Id == 0)
            {
                _context.Roles.Add(userRole);
                await _context.SaveChangesAsync();
            }

            var user = new User
            {
                Username     = dto.Username,
                Email        = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
            await _context.SaveChangesAsync();

            return (user, null, null);
        }

        public async Task<User?> ValidateCredentialsAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;
            return user;
        }

        public async Task<(string Reason, AppealStatus? LastAppealStatus)> GetBanInfoAsync(User user)
        {
            var banNotification = await _context.UserNotifications
                .Where(n => n.UserId == user.Id && n.Type == NotificationType.Ban)
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            var reason = banNotification?.Reason ?? "Brak podanego powodu.";

            var lastBanAppeal = await _context.Appeals
                .Where(a => a.UserId == user.Id && a.Subject == "Odwołanie od blokady konta")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            AppealStatus? appealStatus = null;
            if (lastBanAppeal != null && banNotification != null
                && lastBanAppeal.CreatedAt > banNotification.CreatedAt)
            {
                appealStatus = lastBanAppeal.Status;
            }

            return (reason, appealStatus);
        }

        public async Task<User?> GetProfileAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Reviews)
                .Include(u => u.FavoriteLists)
                    .ThenInclude(fl => fl.FavoriteGames)
                        .ThenInclude(fg => fg.Game)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<List<object>> GetUserArticlesAsync(int userId)
        {
            return await _context.Articles
                .Include(a => a.Game)
                .Where(a => a.AuthorId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => (object)new
                {
                    a.Id, a.Title, a.IsVerified, a.CreatedAt,
                    GameTitle = a.Game.Title,
                    GameId    = a.GameId
                })
                .ToListAsync();
        }

        public async Task<(string? Field, string? Error)> UpdateProfileAsync(int userId, UpdateProfileDto dto)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return ("", "Użytkownik nie istnieje.");

            if (dto.Email != user.Email && await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return ("Email", "Ten email jest już zajęty");

            if (dto.Username != user.Username && await _context.Users.AnyAsync(u => u.Username == dto.Username))
                return ("Username", "Ta nazwa użytkownika jest już zajęta");

            user.Username    = dto.Username;
            user.Email       = dto.Email;
            user.Description = dto.Description;
            await _context.SaveChangesAsync();

            return (null, null);
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(string? Url, string? Error)> UploadAvatarAsync(int userId, IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                return (null, "Dozwolone są tylko pliki .png, .jpg i .jpeg");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "avatars");
            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var url  = $"/images/avatars/{fileName}";
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.ProfilePictureUrl = url;
                await _context.SaveChangesAsync();
            }

            return (url, null);
        }

        public async Task<string> GetRoleNameAsync(int userId)
        {
            return await _context.UserRoles
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .FirstOrDefaultAsync() ?? "User";
        }

        public async Task<User?> FindByIdAsync(int userId)
            => await _context.Users.FindAsync(userId);

        public async Task<List<object>> GetAllGamesForDropdownAsync()
        {
            return await _context.Games
                .OrderBy(g => g.Title)
                .Select(g => (object)new { g.Id, g.Title })
                .ToListAsync();
        }
    }
}
