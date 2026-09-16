using ForgeQA.Domain.Entities;
using ForgeQA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ForgeQA.Infrastructure.Persistence;

public class ForgeQADbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ForgeQADbContext(DbContextOptions<ForgeQADbContext> options) : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Build> Builds => Set<Build>();
    public DbSet<BuildArtifact> BuildArtifacts => Set<BuildArtifact>();
    public DbSet<ArtifactUploadSession> ArtifactUploadSessions => Set<ArtifactUploadSession>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<BugReport> BugReports => Set<BugReport>();
    public DbSet<BugAttachment> BugAttachments => Set<BugAttachment>();
    public DbSet<ProjectApiKey> ProjectApiKeys => Set<ProjectApiKey>();
    public DbSet<TelemetrySession> TelemetrySessions => Set<TelemetrySession>();
    public DbSet<TelemetryEvent> TelemetryEvents => Set<TelemetryEvent>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ForgeQADbContext).Assembly);
    }
}
