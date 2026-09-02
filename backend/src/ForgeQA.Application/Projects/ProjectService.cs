using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Common;
using ForgeQA.Domain.Entities;

namespace ForgeQA.Application.Projects;

public class ProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectService(IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _organizationRepository = organizationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProjectResponse>> CreateAsync(Guid organizationId, Guid userId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var membership = await _organizationRepository.GetMembershipAsync(organizationId, userId, cancellationToken);
        if (membership is null)
            return Result<ProjectResponse>.Failure(ErrorType.Forbidden, "You are not a member of this organization.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ProjectResponse>.Failure(ErrorType.Validation, "Project name is required.");

        var baseSlug = SlugGenerator.Generate(request.Name);
        var slug = await EnsureUniqueSlugAsync(organizationId, baseSlug, cancellationToken);

        var project = new Project(organizationId, request.Name, slug, request.Description);
        _projectRepository.Add(project);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ProjectResponse>.Success(ToResponse(project));
    }

    public async Task<Result<List<ProjectResponse>>> GetForOrganizationAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await _organizationRepository.GetMembershipAsync(organizationId, userId, cancellationToken);
        if (membership is null)
            return Result<List<ProjectResponse>>.Failure(ErrorType.Forbidden, "You are not a member of this organization.");

        var projects = await _projectRepository.GetForOrganizationAsync(organizationId, cancellationToken);
        return Result<List<ProjectResponse>>.Success(projects.Select(ToResponse).ToList());
    }

    public async Task<Result<ProjectResponse>> GetByIdAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return Result<ProjectResponse>.Failure(ErrorType.NotFound, "Project not found.");

        var membership = await _organizationRepository.GetMembershipAsync(project.OrganizationId, userId, cancellationToken);
        if (membership is null)
            return Result<ProjectResponse>.Failure(ErrorType.Forbidden, "You are not a member of this project's organization.");

        return Result<ProjectResponse>.Success(ToResponse(project));
    }

    private async Task<string> EnsureUniqueSlugAsync(Guid organizationId, string baseSlug, CancellationToken cancellationToken)
    {
        var slug = baseSlug;
        var suffix = 1;
        while (await _projectRepository.SlugExistsInOrganizationAsync(organizationId, slug, cancellationToken))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }
        return slug;
    }

    private static ProjectResponse ToResponse(Project project) => new(
        project.Id, project.OrganizationId, project.Name, project.Slug, project.Description, project.CreatedAt, project.UpdatedAt);
}
