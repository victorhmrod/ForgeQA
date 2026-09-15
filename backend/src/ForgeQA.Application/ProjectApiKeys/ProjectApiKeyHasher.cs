using System.Security.Cryptography;
using System.Text;

namespace ForgeQA.Application.ProjectApiKeys;

/// <summary>
/// Generation and one-way hashing for Project API keys. Pure BCL cryptography — no framework
/// dependency — so both the creation path (Application) and the request-authentication path
/// (Infrastructure/Api) can share exactly one implementation instead of two independently
/// "close enough" ones.
/// </summary>
public static class ProjectApiKeyHasher
{
    private const string KeyPrefixTag = "fqa_proj";

    public record GeneratedKey(string PlaintextKey, string Prefix, string Hash);

    public static GeneratedKey Generate()
    {
        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var plaintextKey = $"{KeyPrefixTag}_{prefix}_{secret}";

        return new GeneratedKey(plaintextKey, prefix, Hash(plaintextKey));
    }

    public static string Hash(string plaintextKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Cheap, allocation-free shape check before bothering with a hash + DB lookup.</summary>
    public static bool LooksLikeProjectApiKey(string? candidate) =>
        !string.IsNullOrEmpty(candidate) && candidate.StartsWith(KeyPrefixTag + "_", StringComparison.Ordinal);
}
