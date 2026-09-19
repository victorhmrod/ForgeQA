using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Artifacts;
using ForgeQA.Application.Bugs;
using ForgeQA.Application.Telemetry;
using ForgeQA.Application.Performance;
using ForgeQA.Infrastructure.Auth;
using ForgeQA.Infrastructure.Identity;
using ForgeQA.Infrastructure.Persistence;
using ForgeQA.Infrastructure.Persistence.Repositories;
using ForgeQA.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeQA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ForgeQADbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
            options.UseNpgsql(connectionString);
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ForgeQADbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<ArtifactStorageOptions>(configuration.GetSection(ArtifactStorageOptions.SectionName));
        services.Configure<S3StorageOptions>(configuration.GetSection(ArtifactStorageOptions.SectionName));
        services.Configure<BugReportingOptions>(configuration.GetSection(BugReportingOptions.SectionName));
        services.Configure<TelemetryOptions>(configuration.GetSection(TelemetryOptions.SectionName));
        services.Configure<PerformanceOptions>(configuration.GetSection(PerformanceOptions.SectionName));

        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IBuildRepository, BuildRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IBugRepository, BugRepository>();
        services.AddScoped<IProjectApiKeyRepository, ProjectApiKeyRepository>();
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddScoped<IPerformanceRepository, PerformanceRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<S3ArtifactStorage>();
        services.AddSingleton<IArtifactStorage>(sp => sp.GetRequiredService<S3ArtifactStorage>());
        services.AddSingleton<IObjectStorage>(sp => sp.GetRequiredService<S3ArtifactStorage>());

        return services;
    }
}
