using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;

namespace ForgeQA.Application.Organizations;

public class OrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationService(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public async Task<List<OrganizationResponse>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetForUserAsync(userId, cancellationToken);

        return organizations
            .Select(org => new OrganizationResponse(
                org.Id,
                org.Name,
                org.Slug,
                org.Members.Single(m => m.UserId == userId).Role.ToString(),
                org.CreatedAt,
                org.UpdatedAt))
            .ToList();
    }

    public async Task<Result<OrganizationResponse>> GetByIdAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId, cancellationToken);
        if (organization is null)
            return Result<OrganizationResponse>.Failure(ErrorType.NotFound, "Organization not found.");

        var membership = organization.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null)
            return Result<OrganizationResponse>.Failure(ErrorType.Forbidden, "You are not a member of this organization.");

        return Result<OrganizationResponse>.Success(new OrganizationResponse(
            organization.Id, organization.Name, organization.Slug, membership.Role.ToString(), organization.CreatedAt, organization.UpdatedAt));
    }

    public async Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await _organizationRepository.GetMembershipAsync(organizationId, userId, cancellationToken);
        return membership is not null;
    }
}
