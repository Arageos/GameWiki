using GameWiki.Models;
using Microsoft.EntityFrameworkCore;

public class GameWikiDbContext : DbContext
{
    public GameWikiDbContext(DbContextOptions<GameWikiDbContext> options) : base(options)
    {
    }

    public DbSet<Game> Games { get; set; }
    public DbSet<Genre> Genres { get; set; }
    public DbSet<Platform> Platforms { get; set; }
    public DbSet<GameGenre> GameGenres { get; set; }
    public DbSet<GamePlatform> GamePlatforms { get; set; }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }

    public DbSet<Review> Reviews { get; set; }

    public DbSet<Article> Articles { get; set; }
    public DbSet<ArticleBlock> ArticleBlocks { get; set; }

    public DbSet<Comment> Comments { get; set; }
    public DbSet<CommentReaction> CommentReactions { get; set; }

    public DbSet<FavoriteList> FavoriteLists { get; set; }
    public DbSet<FavoriteGame> FavoriteGames { get; set; }

    public DbSet<Image> Images { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // GameGenre — klucz złożony
        modelBuilder.Entity<GameGenre>()
            .HasKey(x => new { x.GameId, x.GenreId });
        modelBuilder.Entity<GameGenre>()
            .HasOne(x => x.Game)
            .WithMany(x => x.GameGenres)
            .HasForeignKey(x => x.GameId);
        modelBuilder.Entity<GameGenre>()
            .HasOne(x => x.Genre)
            .WithMany(x => x.GameGenres)
            .HasForeignKey(x => x.GenreId);

        // GamePlatform — klucz złożony
        modelBuilder.Entity<GamePlatform>()
            .HasKey(x => new { x.GameId, x.PlatformId });
        modelBuilder.Entity<GamePlatform>()
            .HasOne(x => x.Game)
            .WithMany(x => x.GamePlatforms)
            .HasForeignKey(x => x.GameId);
        modelBuilder.Entity<GamePlatform>()
            .HasOne(x => x.Platform)
            .WithMany(x => x.GamePlatforms)
            .HasForeignKey(x => x.PlatformId);

        // UserRole — klucz złożony
        modelBuilder.Entity<UserRole>()
            .HasKey(x => new { x.UserId, x.RoleId });
        modelBuilder.Entity<UserRole>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);
        modelBuilder.Entity<UserRole>()
            .HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId);

        // FavoriteGame — klucz złożony
        modelBuilder.Entity<FavoriteGame>()
            .HasKey(x => new { x.FavoriteListId, x.GameId });

        // Review
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Game)
            .WithMany(g => g.Reviews)
            .HasForeignKey(r => r.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Article
        modelBuilder.Entity<Article>()
            .HasOne(a => a.Game)
            .WithMany()
            .HasForeignKey(a => a.GameId);
        modelBuilder.Entity<Article>()
            .HasOne(a => a.Author)
            .WithMany()
            .HasForeignKey(a => a.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ArticleBlock
        modelBuilder.Entity<ArticleBlock>()
            .HasOne(b => b.Article)
            .WithMany(a => a.Blocks)
            .HasForeignKey(b => b.ArticleId);

        // Comment — powiązanie z artykułem
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Article)
            .WithMany(a => a.Comments)
            .HasForeignKey(c => c.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comment — zagnieżdżone odpowiedzi
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.ParentComment)
            .WithMany()
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Comment — autor
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // CommentReaction — jeden użytkownik, jedna reakcja na komentarz
        modelBuilder.Entity<CommentReaction>()
            .HasOne(r => r.Comment)
            .WithMany(c => c.Reactions)
            .HasForeignKey(r => r.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CommentReaction>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CommentReaction>()
            .HasIndex(r => new { r.CommentId, r.UserId })
            .IsUnique();

        // Image — powiązanie z grą lub blokiem artykułu
        modelBuilder.Entity<Image>()
            .HasOne<Game>()
            .WithMany()
            .HasForeignKey(i => i.GameId)
            .OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Image>()
            .HasOne<ArticleBlock>()
            .WithMany()
            .HasForeignKey(i => i.ArticleBlockId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}