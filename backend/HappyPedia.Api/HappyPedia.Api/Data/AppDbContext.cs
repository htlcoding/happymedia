using HappyPedia.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HappyPedia.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ArticleVote> Votes => Set<ArticleVote>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Keyword> Keywords => Set<Keyword>();
    public DbSet<RssFeed> RssFeeds => Set<RssFeed>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<ArticleVote>()
            .HasIndex(v => new { v.UserId, v.ArticleId })
            .IsUnique();
    }
}