using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.ProjectApiKeys;

public record CreateProjectApiKeyRequest(string Name, IReadOnlyList<ProjectApiKeyScope>? Scopes);

/// <summary>The only time the plaintext secret is ever available — callers must copy it now.</summary>
public record ProjectApiKeyCreatedResponse(Guid Id, string Name, string Prefix, string PlaintextKey, IReadOnlyList<ProjectApiKeyScope> Scopes, DateTime CreatedAt);

public record ProjectApiKeyResponse(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyList<ProjectApiKeyScope> Scopes,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    DateTime? RevokedAt);
