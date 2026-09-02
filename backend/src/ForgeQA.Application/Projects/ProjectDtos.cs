namespace ForgeQA.Application.Projects;

public record CreateProjectRequest(string Name, string? Description);

public record ProjectResponse(Guid Id, Guid OrganizationId, string Name, string Slug, string? Description, DateTime CreatedAt, DateTime UpdatedAt);
