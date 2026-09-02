namespace ForgeQA.Application.Abstractions;

public record IdentityUserInfo(Guid Id, string Email, string DisplayName, DateTime CreatedAt);

public record AuthTokens(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt);

public enum RegisterResult
{
    Success,
    EmailAlreadyExists,
    InvalidPassword
}

public enum LoginResult
{
    Success,
    InvalidCredentials
}

public enum RefreshResult
{
    Success,
    InvalidOrExpiredToken
}

public interface IIdentityService
{
    Task<(RegisterResult Result, IdentityUserInfo? User, IReadOnlyList<string> Errors)> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken);
    Task<(LoginResult Result, IdentityUserInfo? User)> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);
    Task<IdentityUserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<AuthTokens> IssueTokensAsync(IdentityUserInfo user, CancellationToken cancellationToken);
    Task<(RefreshResult Result, IdentityUserInfo? User, AuthTokens? Tokens)> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
}
