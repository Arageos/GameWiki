using Bogus;
using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

namespace GameWiki.Data
{
    public class DataSeeder
    {
        private readonly GameWikiDbContext _context;

        public DataSeeder(GameWikiDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedReviewsAsync();
            await SeedArticlesAsync();
            await SeedFavoriteListsAsync();
            await SeedReportsAsync();
            Console.WriteLine("✅ Seeding zakończony!");
        }

        private async Task SeedRolesAsync()
        {
            if (await _context.Roles.AnyAsync()) return;

            _context.Roles.AddRange(
                new Role { Name = "User" },
                new Role { Name = "Moderator" },
                new Role { Name = "Admin" }
            );
            await _context.SaveChangesAsync();
            Console.WriteLine("✅ Role dodane");
        }

        private async Task SeedUsersAsync()
        {
            if (await _context.Users.CountAsync() > 3) return;

            var userRole = await _context.Roles.FirstAsync(r => r.Name == "User");

            var faker = new Faker<User>("pl")
                .RuleFor(u => u.Username, f => f.Internet.UserName())
                .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Username))
                .RuleFor(u => u.PasswordHash, f => BCrypt.Net.BCrypt.HashPassword("Test1234!"))
                .RuleFor(u => u.Description, f => f.Lorem.Sentence())
                .RuleFor(u => u.ProfilePictureUrl, f => f.Internet.Avatar())
                .RuleFor(u => u.IsBanned, f => false);

            var users = faker.Generate(20);

            foreach (var user in users)
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
            }

            await _context.SaveChangesAsync();
            Console.WriteLine("✅ Użytkownicy dodani (hasło: Test1234!)");
        }

        private async Task SeedReviewsAsync()
        {
            if (await _context.Reviews.AnyAsync()) return;

            var games = await _context.Games.ToListAsync();
            var users = await _context.Users.ToListAsync();

            if (!games.Any() || !users.Any()) return;

            var reviewContents = new[]
            {
                "Świetna gra, polecam każdemu!",
                "Grafika na wysokim poziomie, ale fabuła mogłaby być lepsza.",
                "Spędziłem przy tej grze setki godzin. Warto!",
                "Przeciętna produkcja, nic specjalnego.",
                "Jedna z najlepszych gier jakie grałem.",
                "Dobra gra na weekend, choć trochę krótka.",
                "Niesamowity klimat i muzyka. Polecam!",
                "Mechaniki walki są rewelacyjne.",
                "Trochę za dużo bugów na premierę.",
                "Klasyk gatunku, must-play!",
                null, null, null // część bez recenzji (tylko ocena)
            };

            var random = new Random(42);
            var usedCombos = new HashSet<(int, int)>();

            foreach (var game in games)
            {
                var reviewCount = random.Next(2, 8);
                var shuffledUsers = users.OrderBy(_ => random.Next()).Take(reviewCount).ToList();

                foreach (var user in shuffledUsers)
                {
                    if (usedCombos.Contains((game.Id, user.Id))) continue;
                    usedCombos.Add((game.Id, user.Id));

                    _context.Reviews.Add(new Review
                    {
                        GameId = game.Id,
                        UserId = user.Id,
                        Rating = random.Next(1, 6),
                        Content = reviewContents[random.Next(reviewContents.Length)],
                        CreatedAt = DateTime.Now.AddDays(-random.Next(1, 365)),
                        IsVerified = random.Next(0, 2) == 1
                    });
                }
            }

            await _context.SaveChangesAsync();
            Console.WriteLine("✅ Recenzje dodane");
        }

        private async Task SeedArticlesAsync()
        {
            if (await _context.Articles.AnyAsync()) return;

            var games = await _context.Games.ToListAsync();
            var users = await _context.Users.ToListAsync();

            if (!games.Any() || !users.Any()) return;

            var faker = new Faker("pl");
            var random = new Random(42);

            foreach (var game in games.Take(10))
            {
                var author = users[random.Next(users.Count)];

                var article = new Article
                {
                    GameId = game.Id,
                    AuthorId = author.Id,
                    Title = $"Przewodnik po grze {game.Title}",
                    IsVerified = true,
                    CreatedAt = DateTime.Now.AddDays(-random.Next(1, 200)),
                    Blocks = new List<ArticleBlock>
                    {
                        new ArticleBlock
                        {
                            Type = ArticleBlockType.Text,
                            Content = faker.Lorem.Paragraphs(2),
                            Order = 1
                        },
                        new ArticleBlock
                        {
                            Type = ArticleBlockType.Text,
                            Content = faker.Lorem.Paragraphs(2),
                            Order = 2
                        }
                    }
                };

                _context.Articles.Add(article);
                await _context.SaveChangesAsync();

                // Dodaj komentarze do artykułu
                var commentCount = random.Next(2, 6);
                for (int i = 0; i < commentCount; i++)
                {
                    _context.Comments.Add(new Comment
                    {
                        ArticleId = article.Id,
                        UserId = users[random.Next(users.Count)].Id,
                        Content = faker.Lorem.Sentence(),
                        CreatedAt = DateTime.Now.AddDays(-random.Next(1, 100)),
                        IsVerified = true
                    });
                }
                await _context.SaveChangesAsync();
            }

            Console.WriteLine("✅ Artykuły i komentarze dodane");
        }

        private async Task SeedFavoriteListsAsync()
        {
            if (await _context.FavoriteLists.AnyAsync()) return;

            var users = await _context.Users.ToListAsync();
            var games = await _context.Games.ToListAsync();

            if (!users.Any() || !games.Any()) return;

            var random = new Random(42);
            var listNames = new[] { "Chcę zagrać", "Już grałem", "Polecam znajomym", "Ulubione" };

            foreach (var user in users.Take(10))
            {
                var list = new FavoriteList
                {
                    UserId = user.Id,
                    Name = listNames[random.Next(listNames.Length)],
                    FavoriteGames = new List<FavoriteGame>()
                };

                var randomGames = games.OrderBy(_ => random.Next()).Take(random.Next(2, 6)).ToList();
                foreach (var game in randomGames)
                {
                    list.FavoriteGames.Add(new FavoriteGame { GameId = game.Id });
                }

                _context.FavoriteLists.Add(list);
            }

            await _context.SaveChangesAsync();
            Console.WriteLine("✅ Listy ulubionych dodane");
        }

        private async Task SeedReportsAsync()
        {
            if (await _context.Reports.AnyAsync()) return;

            var users = await _context.Users.ToListAsync();
            if (users.Count < 2) return;

            var random = new Random(42);
            var reasons = new[]
            {
                "Spam i reklamy",
                "Nieodpowiednia treść",
                "Fałszywe informacje",
                "Obraźliwy język",
                "Naruszenie regulaminu"
            };

            for (int i = 0; i < 10; i++)
            {
                _context.Reports.Add(new Report
                {
                    ReporterId = users[random.Next(users.Count)].Id,
                    Type = (ReportType)random.Next(0, 4),
                    TargetId = random.Next(1, 10),
                    Reason = reasons[random.Next(reasons.Length)],
                    Status = ReportStatus.Pending,
                    CreatedAt = DateTime.Now.AddDays(-random.Next(1, 60))
                });
            }

            await _context.SaveChangesAsync();
            Console.WriteLine("✅ Zgłoszenia dodane");
        }
    }
}