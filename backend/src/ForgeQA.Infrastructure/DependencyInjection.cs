using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Artifacts;
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

        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IBuildRepository, BuildRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<IArtifactStorage, S3ArtifactStorage>();

        return services;
    }
}
