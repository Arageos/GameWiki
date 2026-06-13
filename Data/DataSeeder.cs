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
            //await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedReviewsAsync();
            await SeedArticlesAndCommentsAsync();
            await SeedFavoriteListsAsync();
            await SeedReportsAsync();
            await SeedNotificationsAsync();
            Console.WriteLine("✅ Seeding zakończony!");
        }

        // ─────────────────────────────────────────
        //private async Task SeedRolesAsync()
        //{
        //    if (await _context.Roles.AnyAsync()) return;
        //    _context.Roles.AddRange(
        //        new Role { Name = "User" },
        //        new Role { Name = "Moderator" },
        //        new Role { Name = "Admin" }
        //    );
        //    await _context.SaveChangesAsync();
        //    Console.WriteLine("✅ Role dodane");
        //}

        // ─────────────────────────────────────────
        private async Task SeedUsersAsync()
        {
            if (await _context.Users.CountAsync() > 10) return;

            var userRole = await _context.Roles.FirstAsync(r => r.Name == "User");
            var hash = BCrypt.Net.BCrypt.HashPassword("Test1234!");

            var faker = new Faker<User>("pl")
                .RuleFor(u => u.Username, f => f.Internet.UserName() + f.Random.Number(1000, 9999))
                .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.Username))
                .RuleFor(u => u.PasswordHash, _ => hash)
                .RuleFor(u => u.Description, f => f.Lorem.Sentence())
                .RuleFor(u => u.ProfilePictureUrl, f => f.Internet.Avatar())
                .RuleFor(u => u.IsBanned, f => f.Random.Bool(0.03f));

            var users = faker.Generate(1000);

            // Batch insert po 200
            foreach (var batch in users.Chunk(200))
            {
                await _context.Users.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            // UserRoles batch
            var allUsers = await _context.Users.Select(u => u.Id).ToListAsync();
            var userRoles = allUsers.Select(id => new UserRole
            {
                UserId = id,
                RoleId = userRole.Id
            }).ToList();

            foreach (var batch in userRoles.Chunk(500))
            {
                await _context.UserRoles.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            Console.WriteLine($"✅ 1000 użytkowników dodanych (hasło: Test1234!)");
        }

        // ─────────────────────────────────────────
        private async Task SeedReviewsAsync()
        {
            if (await _context.Reviews.AnyAsync()) return;

            var gameIds = await _context.Games.Select(g => g.Id).ToListAsync();
            var userIds = await _context.Users.Select(u => u.Id).ToListAsync();

            if (!gameIds.Any() || !userIds.Any()) return;

            var contents = new[]
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
                "Zdecydowanie przereklamowana produkcja.",
                "Genialna fabuła, słaba optymalizacja.",
                "Multiplayer wciąga na długie godziny.",
                "Kampania dla jednego gracza to majstersztyk.",
                "Świetny soundtrack, przeciętna grywalność.",
                "Warta swojej ceny w promocji.",
                "Najlepsza gra tego roku bez dwóch zdań.",
                "Rozczarowanie po świetnej poprzedniej części.",
                "Idealna na wieczór z przyjaciółmi.",
                null, null // część tylko z oceną
            };

            var random = new Random(42);
            var usedCombos = new HashSet<(int, int)>();
            var reviews = new List<Review>();

            // 5 recenzji na grę = ~5 005 łącznie
            foreach (var gameId in gameIds)
            {
                var reviewCount = random.Next(4, 7); // 4-6 na grę
                var shuffledUsers = userIds
                    .OrderBy(_ => random.Next())
                    .Take(reviewCount)
                    .ToList();

                foreach (var userId in shuffledUsers)
                {
                    if (usedCombos.Contains((gameId, userId))) continue;
                    usedCombos.Add((gameId, userId));

                    reviews.Add(new Review
                    {
                        GameId = gameId,
                        UserId = userId,
                        Rating = random.Next(1, 6),
                        Content = contents[random.Next(contents.Length)],
                        CreatedAt = DateTime.Now.AddDays(-random.Next(1, 730)),
                        IsVerified = random.Next(0, 10) > 1
                    });
                }
            }

            // Batch insert po 500
            int total = 0;
            foreach (var batch in reviews.Chunk(500))
            {
                await _context.Reviews.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
                total += batch.Length;
                Console.WriteLine($"  → Recenzje: {total}/{reviews.Count}");
            }

            Console.WriteLine($"✅ {reviews.Count} recenzji dodanych");
        }

        // ─────────────────────────────────────────
        private async Task SeedArticlesAndCommentsAsync()
        {
            if (await _context.Articles.AnyAsync()) return;

            var gameIds = await _context.Games
                .Select(g => new { g.Id, g.Title })
                .ToListAsync();
            var userIds = await _context.Users.Select(u => u.Id).ToListAsync();

            if (!gameIds.Any() || !userIds.Any()) return;

            var faker = new Faker("pl");
            var random = new Random(42);

            var titleTemplates = new[]
            {
                "Przewodnik po grze {0}",
                "Poradnik dla początkujących — {0}",
                "Historia i lore — {0}",
                "Sekrety i easter eggi w {0}",
                "Analiza mechanik gry {0}",
                "Poradnik do trofeów — {0}",
                "Najlepsze buildy w {0}",
                "Recenzja — {0}",
                "Wszystko o {0}",
                "Kompletny poradnik — {0}"
            };

            int totalArticles = 0;
            int totalComments = 0;

            // Przetwarzaj po 100 gier na raz
            foreach (var gameBatch in gameIds.Chunk(100))
            {
                var articles = new List<Article>();

                foreach (var game in gameBatch)
                {
                    var articleCount = random.Next(1, 3); // 1-2 artykuły

                    for (int i = 0; i < articleCount; i++)
                    {
                        var title = string.Format(
                            titleTemplates[random.Next(titleTemplates.Length)],
                            game.Title);

                        articles.Add(new Article
                        {
                            GameId = game.Id,
                            AuthorId = userIds[random.Next(userIds.Count)],
                            Title = title,
                            IsVerified = random.Next(0, 10) > 2,
                            CreatedAt = DateTime.Now.AddDays(-random.Next(1, 500)),
                            Blocks = new List<ArticleBlock>
                            {
                                new ArticleBlock
                                {
                                    Type = ArticleBlockType.Text,
                                    Content = faker.Lorem.Paragraphs(random.Next(2, 4)),
                                    Order = 1
                                },
                                new ArticleBlock
                                {
                                    Type = ArticleBlockType.Text,
                                    Content = faker.Lorem.Paragraphs(random.Next(2, 4)),
                                    Order = 2
                                }
                            }
                        });
                    }
                }

                await _context.Articles.AddRangeAsync(articles);
                await _context.SaveChangesAsync();
                totalArticles += articles.Count;

                // Komentarze do tych artykułów
                var articleIds = articles.Select(a => a.Id).ToList();
                var comments = new List<Comment>();

                foreach (var articleId in articleIds)
                {
                    var commentCount = random.Next(5, 11); // 5-10 komentarzy

                    for (int i = 0; i < commentCount; i++)
                    {
                        comments.Add(new Comment
                        {
                            ArticleId = articleId,
                            UserId = userIds[random.Next(userIds.Count)],
                            Content = faker.Lorem.Sentence(random.Next(5, 25)),
                            CreatedAt = DateTime.Now.AddDays(-random.Next(1, 300)),
                            IsVerified = random.Next(0, 10) > 1,
                            ParentCommentId = null
                        });
                    }
                }

                await _context.Comments.AddRangeAsync(comments);
                await _context.SaveChangesAsync();
                totalComments += comments.Count;

                Console.WriteLine($"  → Artykuły: {totalArticles} | Komentarze: {totalComments}");
            }

            Console.WriteLine($"✅ {totalArticles} artykułów i {totalComments} komentarzy dodanych");
        }

        // ─────────────────────────────────────────
        private async Task SeedFavoriteListsAsync()
        {
            if (await _context.FavoriteLists.AnyAsync()) return;

            var userIds = await _context.Users.Select(u => u.Id).ToListAsync();
            var gameIds = await _context.Games.Select(g => g.Id).ToListAsync();

            if (!userIds.Any() || !gameIds.Any()) return;

            var random = new Random(42);
            var listNames = new[]
            {
                "Chcę zagrać", "Już grałem", "Polecam znajomym",
                "Ulubione", "Arcydzieła", "Do sprawdzenia", "Top 10"
            };

            var lists = new List<FavoriteList>();

            foreach (var userId in userIds)
            {
                var listCount = random.Next(1, 3); // 1-2 listy
                for (int i = 0; i < listCount; i++)
                {
                    var randomGames = gameIds
                        .OrderBy(_ => random.Next())
                        .Take(random.Next(3, 10))
                        .Select(gId => new FavoriteGame { GameId = gId })
                        .ToList();

                    lists.Add(new FavoriteList
                    {
                        UserId = userId,
                        Name = listNames[random.Next(listNames.Length)],
                        FavoriteGames = randomGames
                    });
                }
            }

            foreach (var batch in lists.Chunk(200))
            {
                await _context.FavoriteLists.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            Console.WriteLine($"✅ {lists.Count} list ulubionych dodanych");
        }

        // ─────────────────────────────────────────
        private async Task SeedReportsAsync()
        {
            if (await _context.Reports.AnyAsync()) return;

            var userIds = await _context.Users.Select(u => u.Id).ToListAsync();
            var reviewIds = await _context.Reviews.Select(r => r.Id).ToListAsync();
            var commentIds = await _context.Comments.Select(c => c.Id).ToListAsync();

            if (userIds.Count < 2) return;

            var random = new Random(42);
            var reasons = new[]
            {
                "Spam i reklamy", "Nieodpowiednia treść",
                "Fałszywe informacje", "Obraźliwy język",
                "Naruszenie regulaminu", "Mowa nienawiści",
                "Treści dla dorosłych", "Wprowadzanie w błąd"
            };

            var reports = new List<Report>();

            for (int i = 0; i < 500; i++)
            {
                var type = (ReportType)random.Next(0, 4);
                var targetId = type switch
                {
                    ReportType.Review => reviewIds.Any() ? reviewIds[random.Next(reviewIds.Count)] : 1,
                    ReportType.Comment => commentIds.Any() ? commentIds[random.Next(commentIds.Count)] : 1,
                    _ => userIds[random.Next(userIds.Count)]
                };

                reports.Add(new Report
                {
                    ReporterId = userIds[random.Next(userIds.Count)],
                    Type = type,
                    TargetId = targetId,
                    Reason = reasons[random.Next(reasons.Length)],
                    Status = (ReportStatus)random.Next(0, 3),
                    CreatedAt = DateTime.Now.AddDays(-random.Next(1, 180))
                });
            }

            await _context.Reports.AddRangeAsync(reports);
            await _context.SaveChangesAsync();
            Console.WriteLine($"✅ {reports.Count} zgłoszeń dodanych");
        }

        // ─────────────────────────────────────────
        private async Task SeedNotificationsAsync()
        {
            if (await _context.UserNotifications.AnyAsync()) return;

            var userIds = await _context.Users.Select(u => u.Id).ToListAsync();
            if (!userIds.Any()) return;

            var random = new Random(42);
            var messages = new[]
            {
                "Twoja recenzja została zweryfikowana.",
                "Ktoś skomentował Twój artykuł.",
                "Twoje zgłoszenie zostało rozpatrzone.",
                "Nowa odpowiedź na Twój komentarz.",
                "Twój artykuł oczekuje na weryfikację.",
                "Zostałeś wymieniony w komentarzu.",
                "Twoja recenzja otrzymała reakcję.",
                "Nowy artykuł o grze z Twojej listy ulubionych.",
                "Twoje konto zostało zweryfikowane."
            };

            var notifications = new List<UserNotification>();

            foreach (var userId in userIds)
            {
                var count = random.Next(2, 8);
                for (int i = 0; i < count; i++)
                {
                    notifications.Add(new UserNotification
                    {
                        UserId = userId,
                        Message = messages[random.Next(messages.Length)],
                        IsRead = random.Next(0, 10) > 4,
                        CreatedAt = DateTime.Now.AddDays(-random.Next(1, 90))
                    });
                }
            }

            foreach (var batch in notifications.Chunk(500))
            {
                await _context.UserNotifications.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            Console.WriteLine($"✅ {notifications.Count} powiadomień dodanych");
        }
    }
}