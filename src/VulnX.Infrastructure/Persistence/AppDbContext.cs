using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VulnX.Domain.Entities;
using VulnX.Infrastructure.Identity;

namespace VulnX.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ScanTarget> ScanTargets => Set<ScanTarget>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<ScanModule> ScanModules => Set<ScanModule>();
    public DbSet<VulnerabilityDefinition> VulnerabilityDefinitions => Set<VulnerabilityDefinition>();
    public DbSet<ScanResult> ScanResults => Set<ScanResult>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<Tag> Tags => Set<Tag>();

    // Store every enum as a readable string instead of an int.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(200);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Ignore(x => x.IsActive);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ScanTarget>(e =>
        {
            e.Property(x => x.NormalizedUrl).HasMaxLength(2048).IsRequired();
            e.Property(x => x.Hostname).HasMaxLength(255).IsRequired();
            e.Property(x => x.ScopePolicy).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.UserId, x.NormalizedUrl }).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Scan>(e =>
        {
            e.Property(x => x.Error).HasMaxLength(2000);
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            e.HasIndex(x => x.Status);
            e.Property(x => x.Label).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasMany(x => x.Tags).WithMany(t => t.Scans);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Target).WithMany(t => t.Scans).HasForeignKey(x => x.ScanTargetId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Modules).WithOne().HasForeignKey(m => m.ScanId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Results).WithOne().HasForeignKey(r => r.ScanId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ScanModule>(e =>
        {
            e.Property(x => x.ModuleName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Error).HasMaxLength(2000);
        });

        b.Entity<VulnerabilityDefinition>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasMaxLength(100).IsRequired();
        });

        b.Entity<ScanResult>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.AffectedUrl).HasMaxLength(2048);
            e.Property(x => x.DetectionModule).HasMaxLength(100);
            e.Property(x => x.RiskScore).HasPrecision(4, 1);
            e.Property(x => x.ResponseMetadataJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ScanId, x.Severity });
            e.HasOne(x => x.Definition).WithMany().HasForeignKey(x => x.VulnerabilityDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Report>(e =>
        {
            e.Property(x => x.ReportNumber).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.ReportNumber).IsUnique();
            e.Property(x => x.VerificationHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.MetadataJson).HasColumnType("jsonb");
            e.HasOne<Scan>().WithMany().HasForeignKey(x => x.ScanId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AiConversation>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ScanId });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Scan>().WithMany().HasForeignKey(x => x.ScanId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => new { x.UserId, x.ReadAtUtc });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.SafeMetadataJson).HasColumnType("jsonb");
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<Tag>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(2000).IsRequired();
        });
    }
}



