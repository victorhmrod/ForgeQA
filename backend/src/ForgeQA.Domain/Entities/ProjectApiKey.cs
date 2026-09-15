using ForgeQA.Domain.Common;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Domain.Entities;

/// <summary>
/// A scoped, revocable machine credential that authenticates runtime submissions (e.g. the
/// Unreal plugin) for exactly one Project. This is never a user account and is never sufficient
/// to perform Project administration — see <see cref="ProjectApiKeyScope"/> for exactly what it
/// can do. Only <see cref="KeyHash"/> is ever persisted; the plaintext secret is generated,
/// returned to the caller once, and never stored or recoverable again.
/// </summary>
public class ProjectApiKey : Entity
{
    public const int NameMaxLength = 100;
    public const int PrefixMaxLength = 32;
    public const int KeyHashMaxLength = 128;

    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Prefix { get; private set; } = null!;
    public string KeyHash { get; private set; } = null!;

    private readonly List<ProjectApiKeyScope> _scopes = new();
    public IReadOnlyCollection<ProjectApiKeyScope> Scopes => _scopes.AsReadOnly();

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive => !IsRevoked;

    private ProjectApiKey() { }

    public ProjectApiKey(
        Guid projectId,
        string name,
        string prefix,
        string keyHash,
        IEnumerable<ProjectApiKeyScope> scopes,
        Guid createdByUserId)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        ProjectId = projectId;
        CreatedByUserId = createdByUserId;

        SetName(name);
        SetPrefix(prefix);
        SetKeyHash(keyHash);

        var distinctScopes = scopes.Distinct().ToList();
        if (distinctScopes.Count == 0)
            throw new ArgumentException("At least one scope is required.", nameof(scopes));
        _scopes.AddRange(distinctScopes);

        CreatedAt = DateTime.UtcNow;
    }

    public bool HasScope(ProjectApiKeyScope scope) => IsActive && _scopes.Contains(scope);

    public void MarkUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        RevokedAt ??= DateTime.UtcNow;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (name.Length > NameMaxLength)
            throw new ArgumentException($"Name must be at most {NameMaxLength} characters.", nameof(name));

        Name = name.Trim();
    }

    private void SetPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix is required.", nameof(prefix));
        if (prefix.Length > PrefixMaxLength)
            throw new ArgumentException($"Prefix must be at most {PrefixMaxLength} characters.", nameof(prefix));

        Prefix = prefix.Trim();
    }

    private void SetKeyHash(string keyHash)
    {
        if (string.IsNullOrWhiteSpace(keyHash))
            throw new ArgumentException("Key hash is required.", nameof(keyHash));
        if (keyHash.Length > KeyHashMaxLength)
            throw new ArgumentException($"Key hash must be at most {KeyHashMaxLength} characters.", nameof(keyHash));

        KeyHash = keyHash;
    }
}
