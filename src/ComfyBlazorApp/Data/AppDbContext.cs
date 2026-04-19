using Microsoft.EntityFrameworkCore;

namespace ComfyBlazorApp.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PromptPreset>   PromptPresets   => Set<PromptPreset>();
    public DbSet<BatchJob>       BatchJobs       => Set<BatchJob>();
    public DbSet<BatchItem>      BatchItems      => Set<BatchItem>();
    public DbSet<SourceImage>    SourceImages    => Set<SourceImage>();
    public DbSet<PromptHistory>  PromptHistories => Set<PromptHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PromptPreset>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Prompt).HasMaxLength(4000).IsRequired();
            e.Property(x => x.NegativePrompt).HasMaxLength(4000);
            e.Property(x => x.Tags).HasMaxLength(1000);
            e.Property(x => x.ImagePath).HasMaxLength(500);
            e.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<BatchJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<BatchItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Mode).HasMaxLength(20).IsRequired().HasDefaultValue("Generate");
            e.Property(x => x.PromptId).HasMaxLength(100);
            e.Property(x => x.OutputFolder).HasMaxLength(500);
            e.Property(x => x.OutputFileNames).HasMaxLength(4000);
            e.Property(x => x.SourceImagePath).HasMaxLength(500);
            e.Property(x => x.Error).HasMaxLength(2000);

            e.HasOne(x => x.BatchJob)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.BatchJobId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.PromptPreset)
                .WithMany(x => x.BatchItems)
                .HasForeignKey(x => x.PromptPresetId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.BatchJobId);
            e.HasIndex(x => x.PromptId);
        });

        modelBuilder.Entity<SourceImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
            e.Property(x => x.StoredFileName).HasMaxLength(200).IsRequired();
            e.Property(x => x.OriginalFileName).HasMaxLength(200);
            e.HasIndex(x => x.StoredFileName).IsUnique();
        });

        modelBuilder.Entity<PromptHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Prompt).HasMaxLength(4000).IsRequired();
            e.Property(x => x.NegativePrompt).HasMaxLength(4000);
            e.Property(x => x.Mode).HasMaxLength(20).IsRequired().HasDefaultValue("Generate");
            e.Property(x => x.SourceImagePath).HasMaxLength(500);
            e.Property(x => x.ComfyPromptId).HasMaxLength(100);
            e.HasIndex(x => x.CreatedAt);
        });
    }
}
