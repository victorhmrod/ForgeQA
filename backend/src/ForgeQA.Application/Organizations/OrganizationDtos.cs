namespace ForgeQA.Application.Organizations;

public record OrganizationResponse(Guid Id, string Name, string Slug, string Role, DateTime CreatedAt, DateTime UpdatedAt);
