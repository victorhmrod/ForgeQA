using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Bugs;

/// <summary>
/// Who is making a bug-reporting call: either an authenticated ForgeQA user (dashboard/JWT) or a
/// Project-scoped runtime API key. Exactly one of the two shapes is populated — never both.
/// </summary>
public sealed class BugReportAuthor
{
    public Guid? UserId { get; }
    public Guid? ApiKeyId { get; }
    public Guid? ApiKeyProjectId { get; }
    private readonly IReadOnlyCollection<ProjectApiKeyScope> _apiKeyScopes;

    private BugReportAuthor(Guid? userId, Guid? apiKeyId, Guid? apiKeyProjectId, IReadOnlyCollection<ProjectApiKeyScope>? apiKeyScopes)
    {
        UserId = userId;
        ApiKeyId = apiKeyId;
        ApiKeyProjectId = apiKeyProjectId;
        _apiKeyScopes = apiKeyScopes ?? Array.Empty<ProjectApiKeyScope>();
    }

    public static BugReportAuthor FromUser(Guid userId) => new(userId, null, null, null);

    public static BugReportAuthor FromApiKey(Guid apiKeyId, Guid apiKeyProjectId, IReadOnlyCollection<ProjectApiKeyScope> scopes) =>
        new(null, apiKeyId, apiKeyProjectId, scopes);

    public bool IsApiKey => ApiKeyId is not null;

    /// <summary>Always true for a dashboard user — scope enforcement only applies to API keys.</summary>
    public bool HasApiKeyScope(ProjectApiKeyScope scope) => !IsApiKey || _apiKeyScopes.Contains(scope);
}
