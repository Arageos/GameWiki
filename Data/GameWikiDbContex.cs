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
    public DbSet<Section> Sections { get; set; }


    public DbSet<Comment> Comments { get; set; }


    public DbSet<FavoriteList> FavoriteLists { get; set; }
    public DbSet<FavoriteGame> FavoriteGames { get; set; }


    public DbSet<Image> Images { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


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


        modelBuilder.Entity<FavoriteGame>()
            .HasKey(x => new { x.FavoriteListId, x.GameId });


        modelBuilder.Entity<Comment>()
            .HasOne(c => c.ParentComment)
            .WithMany()
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<Review>()
            .HasOne(r => r.Game)
            .WithMany()
            .HasForeignKey(r => r.GameId);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId);


        modelBuilder.Entity<Article>()
            .HasOne(a => a.Game)
            .WithMany()
            .HasForeignKey(a => a.GameId);


        modelBuilder.Entity<Section>()
            .HasOne(s => s.Article)
            .WithMany()
            .HasForeignKey(s => s.ArticleId);


        modelBuilder.Entity<Image>()
            .HasOne<Game>()
            .WithMany()
            .HasForeignKey(i => i.GameId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Image>()
            .HasOne<Article>()
            .WithMany()
            .HasForeignKey(i => i.ArticleId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
