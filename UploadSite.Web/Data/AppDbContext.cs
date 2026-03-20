using Microsoft.EntityFrameworkCore;
using UploadSite.Web.Models;

namespace UploadSite.Web.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.Property(x => x.UserName).HasMaxLength(128);
            entity.Property(x => x.PasswordHash).HasMaxLength(256);
        });

        modelBuilder.Entity<ImportJob>(entity =>
        {
            entity.HasIndex(x => x.BatchId);
            entity.Property(x => x.OriginalFileName).HasMaxLength(260);
            entity.Property(x => x.StoredFileName).HasMaxLength(260);
            entity.Property(x => x.StagingRelativePath).HasMaxLength(512);
            entity.Property(x => x.ContentType).HasMaxLength(128);
            entity.Property(x => x.Artist).HasMaxLength(256);
            entity.Property(x => x.AlbumArtist).HasMaxLength(256);
            entity.Property(x => x.Album).HasMaxLength(256);
            entity.Property(x => x.Title).HasMaxLength(256);
            entity.Property(x => x.Genre).HasMaxLength(128);
            entity.Property(x => x.DuplicateSummary).HasMaxLength(2048);
            entity.Property(x => x.TargetRelativePath).HasMaxLength(512);

            entity.HasOne(x => x.UploadedByUser)
                .WithMany(x => x.ImportJobs)
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
