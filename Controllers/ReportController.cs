using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GameWiki.Controllers
{
    [Authorize(Roles = "Admin,Moderator")]
    public class ReportController : Controller
    {
        private readonly GameWikiDbContext _context;

        public ReportController(GameWikiDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Generate(DateTime? from, DateTime? to, int? days)
        {
            DateTime dateTo = DateTime.UtcNow;
            DateTime dateFrom;

            if (days.HasValue)
            {
                dateFrom = dateTo.AddDays(-days.Value);
            }
            else if (from.HasValue && to.HasValue)
            {
                dateFrom = from.Value.ToUniversalTime();
                dateTo = to.Value.ToUniversalTime();
            }
            else
            {
                dateFrom = dateTo.AddDays(-30);
            }

            // Dane do raportu
            var newGames = await _context.Games
                .Where(g => g.ReleaseDate >= dateFrom && g.ReleaseDate <= dateTo)
                .OrderByDescending(g => g.ReleaseDate)
                .Select(g => new { g.Title, g.ReleaseDate })
                .ToListAsync();

            var newArticles = await _context.Articles
                .Include(a => a.Author)
                .Include(a => a.Game)
                .Where(a => a.CreatedAt >= dateFrom && a.CreatedAt <= dateTo)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new { a.Title, AuthorName = a.Author.Username, GameTitle = a.Game.Title, a.CreatedAt, a.IsVerified })
                .ToListAsync();

            var newComments = await _context.Comments
                .Where(c => c.CreatedAt >= dateFrom && c.CreatedAt <= dateTo)
                .CountAsync();

            var newUsers = await _context.Users
                .CountAsync();

            var userActivity = await _context.Users
                .Select(u => new
                {
                    u.Username,
                    Articles = _context.Articles.Count(a => a.AuthorId == u.Id && a.CreatedAt >= dateFrom && a.CreatedAt <= dateTo),
                    Comments = _context.Comments.Count(c => c.UserId == u.Id && c.CreatedAt >= dateFrom && c.CreatedAt <= dateTo)
                })
                .Where(u => u.Articles > 0 || u.Comments > 0)
                .OrderByDescending(u => u.Articles + u.Comments)
                .Take(20)
                .ToListAsync();

            // Licencja QuestPDF (community)
            QuestPDF.Settings.License = LicenseType.Community;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("GameWiki").FontSize(26).Bold().FontColor("#7c3aed");
                                c.Item().Text("Raport aktywności serwisu").FontSize(13).FontColor("#64748b");
                            });
                            row.ConstantItem(180).AlignRight().Column(c =>
                            {
                                c.Item().Text($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor("#94a3b8");
                                c.Item().Text($"Okres: {dateFrom:dd.MM.yyyy} – {dateTo:dd.MM.yyyy}").FontSize(9).FontColor("#94a3b8");
                            });
                        });
                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#e2e8f0");
                    });

                    page.Content().PaddingTop(16).Column(col =>
                    {
                        // Kafelki podsumowania
                        col.Item().Text("Podsumowanie okresu").FontSize(14).Bold().FontColor("#1e293b");
                        col.Item().PaddingTop(8).Row(row =>
                        {
                            void Tile(RowDescriptor r, string label, string value, string color)
                            {
                                r.RelativeItem().Border(1).BorderColor("#e2e8f0").Padding(10).Column(c =>
                                {
                                    c.Item().Text(value).FontSize(22).Bold().FontColor(color);
                                    c.Item().Text(label).FontSize(9).FontColor("#64748b");
                                });
                            }

                            Tile(row, "Nowe gry", newGames.Count.ToString(), "#7c3aed");
                            row.ConstantItem(8);
                            Tile(row, "Nowe artykuły", newArticles.Count.ToString(), "#0ea5e9");
                            row.ConstantItem(8);
                            Tile(row, "Nowe komentarze", newComments.ToString(), "#10b981");
                            row.ConstantItem(8);
                            Tile(row, "Użytkownicy (łącznie)", newUsers.ToString(), "#f59e0b");
                        });

                        col.Item().PaddingTop(20);

                        // Lista nowych gier
                        col.Item().Text("Nowe gry w okresie").FontSize(14).Bold().FontColor("#1e293b");
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Tytuł").Bold().FontSize(10);
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Data wydania").Bold().FontSize(10);
                            });

                            if (newGames.Any())
                            {
                                foreach (var g in newGames)
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text(g.Title).FontSize(10);
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text(g.ReleaseDate.ToString("dd.MM.yyyy")).FontSize(10);
                                }
                            }
                            else
                            {
                                table.Cell().ColumnSpan(2).Padding(6).Text("Brak nowych gier w tym okresie.").FontSize(10).FontColor("#94a3b8").Italic();
                            }
                        });

                        col.Item().PaddingTop(20);

                        // Lista nowych artykułów
                        col.Item().Text("Nowe artykuły w okresie").FontSize(14).Bold().FontColor("#1e293b");
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(1);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Tytuł").Bold().FontSize(10);
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Autor").Bold().FontSize(10);
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Gra").Bold().FontSize(10);
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Status").Bold().FontSize(10);
                            });

                            if (newArticles.Any())
                            {
                                foreach (var a in newArticles)
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text(a.Title).FontSize(10);
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text(a.AuthorName).FontSize(10);
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text(a.GameTitle).FontSize(10);
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6)
                                        .Text(a.IsVerified ? "✓ Zweryfik." : "⏳ Oczekuje")
                                        .FontSize(9)
                                        .FontColor(a.IsVerified ? "#10b981" : "#f59e0b");
                                }
                            }
                            else
                            {
                                table.Cell().ColumnSpan(4).Padding(6).Text("Brak nowych artykułów w tym okresie.").FontSize(10).FontColor("#94a3b8").Italic();
                            }
                        });

                        col.Item().PaddingTop(20);

                        // Tabela aktywności użytkowników
                        col.Item().Text("Aktywność użytkowników").FontSize(14).Bold().FontColor("#1e293b");
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Użytkownik").Bold().FontSize(10);
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Artykuły").Bold().FontSize(10);
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Komentarze").Bold().FontSize(10);
                                h.Cell().Background("#f1f5f9").Padding(6).Text("Łącznie").Bold().FontSize(10);
                            });

                            if (userActivity.Any())
                            {
                                foreach (var u in userActivity)
                                {
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text(u.Username).FontSize(10);
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text(u.Articles.ToString()).FontSize(10);
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text(u.Comments.ToString()).FontSize(10);
                                    table.Cell().BorderBottom(1).BorderColor("#f1f5f9").Padding(6).Text((u.Articles + u.Comments).ToString()).Bold().FontSize(10);
                                }
                            }
                            else
                            {
                                table.Cell().ColumnSpan(4).Padding(6).Text("Brak aktywności w tym okresie.").FontSize(10).FontColor("#94a3b8").Italic();
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("GameWiki · Raport wygenerowany automatycznie · Strona ").FontSize(9).FontColor("#94a3b8");
                        t.CurrentPageNumber().FontSize(9).FontColor("#94a3b8");
                        t.Span(" z ").FontSize(9).FontColor("#94a3b8");
                        t.TotalPages().FontSize(9).FontColor("#94a3b8");
                    });
                });
            });

            var pdfBytes = pdf.GeneratePdf();
            var fileName = $"GameWiki_Raport_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}