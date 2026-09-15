using ForgeQA.Application.Auth;
using ForgeQA.Application.Builds;
using ForgeQA.Application.Organizations;
using ForgeQA.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeQA.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<OrganizationService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<BuildService>();
        return services;
    }
}
