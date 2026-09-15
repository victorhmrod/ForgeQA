using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.ProjectApiKeys;

public class ProjectApiKeyService
{
    private static readonly ProjectApiKeyScope[] DefaultScopes = { ProjectApiKeyScope.BUG_REPORT_WRITE };

    private readonly IProjectApiKeyRepository _apiKeyRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectApiKeyService(
        IProjectApiKeyRepository apiKeyRepository,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        IUnitOfWork unitOfWork)
    {
        _apiKeyRepository = apiKeyRepository;
        _projectRepository = projectRepository;
        _organizationRepository = organizationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProjectApiKeyCreatedResponse>> CreateAsync(Guid projectId, Guid userId, CreateProjectApiKeyRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<ProjectApiKeyCreatedResponse>.Failure(access.ErrorType, access.Error!);

        var scopes = request.Scopes is { Count: > 0 } ? request.Scopes : DefaultScopes;
        var generated = ProjectApiKeyHasher.Generate();

        ProjectApiKey apiKey;
        try
        {
            apiKey = new ProjectApiKey(projectId, request.Name, generated.Prefix, generated.Hash, scopes, userId);
        }
        catch (ArgumentException ex)
        {
            return Result<ProjectApiKeyCreatedResponse>.Failure(ErrorType.Validation, ex.Message);
        }

        _apiKeyRepository.Add(apiKey);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ProjectApiKeyCreatedResponse>.Success(new ProjectApiKeyCreatedResponse(
            apiKey.Id, apiKey.Name, apiKey.Prefix, generated.PlaintextKey, apiKey.Scopes.ToList(), apiKey.CreatedAt));
    }

    public async Task<Result<List<ProjectApiKeyResponse>>> ListAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<List<ProjectApiKeyResponse>>.Failure(access.ErrorType, access.Error!);

        var keys = await _apiKeyRepository.ListForProjectAsync(projectId, cancellationToken);
        return Result<List<ProjectApiKeyResponse>>.Success(keys.Select(ToResponse).ToList());
    }

    public async Task<Result<ProjectApiKeyResponse>> RevokeAsync(Guid projectId, Guid userId, Guid keyId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<ProjectApiKeyResponse>.Failure(access.ErrorType, access.Error!);

        var key = await _apiKeyRepository.GetByIdForProjectAsync(projectId, keyId, cancellationToken);
        if (key is null)
            return Result<ProjectApiKeyResponse>.Failure(ErrorType.NotFound, "API key not found.");

        key.Revoke();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ProjectApiKeyResponse>.Success(ToResponse(key));
    }

    private async Task<Result<bool>> AuthorizeAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return Result<bool>.Failure(ErrorType.NotFound, "Project not found.");

        var membership = await _organizationRepository.GetMembershipAsync(project.OrganizationId, userId, cancellationToken);
        if (membership is null)
            return Result<bool>.Failure(ErrorType.Forbidden, "You are not a member of this project's organization.");

        return Result<bool>.Success(true);
    }

    private static ProjectApiKeyResponse ToResponse(ProjectApiKey key) => new(
        key.Id, key.Name, key.Prefix, key.Scopes.ToList(), key.CreatedAt, key.LastUsedAt, key.RevokedAt);
}
