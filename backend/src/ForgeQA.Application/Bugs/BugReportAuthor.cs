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

    private BugReportAuthor(Guid? userId, Guid? apiKeyId, Guid? apiKeyProjectId)
    {
        UserId = userId;
        ApiKeyId = apiKeyId;
        ApiKeyProjectId = apiKeyProjectId;
    }

    public static BugReportAuthor FromUser(Guid userId) => new(userId, null, null);

    public static BugReportAuthor FromApiKey(Guid apiKeyId, Guid apiKeyProjectId) => new(null, apiKeyId, apiKeyProjectId);

    public bool IsApiKey => ApiKeyId is not null;
}
