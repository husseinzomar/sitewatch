using Microsoft.EntityFrameworkCore;
using SiteWatch.Core.Entities;

namespace SiteWatch.Infra;

public class SiteWatchDbContext(DbContextOptions<SiteWatchDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Check> Checks => Set<Check>();
    public DbSet<CheckResult> CheckResults => Set<CheckResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Email).HasMaxLength(320);
            entity.Property(u => u.PasswordHash).HasMaxLength(200);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.Property(s => s.Name).HasMaxLength(200);
            entity.Property(s => s.Url).HasMaxLength(2048);

            entity.HasOne(s => s.User)
                .WithMany(u => u.Sites)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Check>(entity =>
        {
            entity.Property(c => c.Type).HasConversion<int>();

            entity.HasOne(c => c.Site)
                .WithMany(s => s.Checks)
                .HasForeignKey(c => c.SiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CheckResult>(entity =>
        {
            entity.Property(r => r.Status).HasConversion<int>();
            entity.HasIndex(r => new { r.CheckId, r.RanAt });

            entity.HasOne(r => r.Check)
                .WithMany(c => c.CheckResults)
                .HasForeignKey(r => r.CheckId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
