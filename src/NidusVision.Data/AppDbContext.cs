using Microsoft.EntityFrameworkCore;
using NidusVision.Core.Models;

namespace NidusVision.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<RecordingSegment> RecordingSegments => Set<RecordingSegment>();
    public DbSet<DetectionEvent> DetectionEvents => Set<DetectionEvent>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<LocalUser> LocalUsers => Set<LocalUser>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Camera>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Location).HasMaxLength(128);
            entity.Property(e => e.MainRtspUrl).HasMaxLength(1024);
            entity.Property(e => e.SubRtspUrl).HasMaxLength(1024);
            entity.Property(e => e.LastResolution).HasMaxLength(32);
            entity.HasIndex(e => e.Name);
        });

        modelBuilder.Entity<RecordingSegment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Path).HasMaxLength(2048);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(2048);
            entity.HasIndex(e => new { e.CameraId, e.StartUtc });
            entity.HasIndex(e => new { e.HasHuman, e.EndUtc });
            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => e.IsFinalized);
            entity.HasOne(e => e.Camera)
                .WithMany(c => c.Segments)
                .HasForeignKey(e => e.CameraId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DetectionEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClipPath).HasMaxLength(2048);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(2048);
            entity.HasIndex(e => new { e.CameraId, e.StartUtc });
            entity.HasIndex(e => e.Confidence);
            entity.HasOne(e => e.Camera)
                .WithMany(c => c.Detections)
                .HasForeignKey(e => e.CameraId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSettings>(entity => entity.HasKey(e => e.Id));
        modelBuilder.Entity<LocalUser>(entity => entity.HasKey(e => e.Id));
    }
}
