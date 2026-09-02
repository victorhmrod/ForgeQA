using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ForgeQA.Application.Abstractions;
using ForgeQA.Infrastructure.Identity;
using ForgeQA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ForgeQA.Infrastructure.Auth;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ForgeQADbContext _dbContext;
    private readonly JwtOptions _jwtOptions;

    public IdentityService(UserManager<ApplicationUser> userManager, ForgeQADbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<(RegisterResult Result, IdentityUserInfo? User, IReadOnlyList<string> Errors)> RegisterAsync(
        string email, string displayName, string password, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            return (RegisterResult.EmailAlreadyExists, null, Array.Empty<string>());

        var now = DateTime.UtcNow;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            CreatedAt = now,
            UpdatedAt = now
        };

        var identityResult = await _userManager.CreateAsync(user, password);
        if (!identityResult.Succeeded)
            return (RegisterResult.InvalidPassword, null, identityResult.Errors.Select(e => e.Description).ToList());

        return (RegisterResult.Success, ToUserInfo(user), Array.Empty<string>());
    }

    public async Task<(LoginResult Result, IdentityUserInfo? User)> ValidateCredentialsAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return (LoginResult.InvalidCredentials, null);

        var validPassword = await _userManager.CheckPasswordAsync(user, password);
        if (!validPassword)
            return (LoginResult.InvalidCredentials, null);

        return (LoginResult.Success, ToUserInfo(user));
    }

    public async Task<IdentityUserInfo?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : ToUserInfo(user);
    }

    public async Task<AuthTokens> IssueTokensAsync(IdentityUserInfo user, CancellationToken cancellationToken)
    {
        var accessToken = GenerateAccessToken(user, out var accessExpiresAt);
        var (refreshToken, refreshExpiresAt) = await GenerateRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthTokens(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt);
    }

    public async Task<(RefreshResult Result, IdentityUserInfo? User, AuthTokens? Tokens)> RefreshTokensAsync(
        string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = Hash(refreshToken);
        var stored = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is null || !stored.IsActive)
            return (RefreshResult.InvalidOrExpiredToken, null, null);

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
            return (RefreshResult.InvalidOrExpiredToken, null, null);

        stored.RevokedAt = DateTime.UtcNow;

        var userInfo = ToUserInfo(user);
        var accessToken = GenerateAccessToken(userInfo, out var accessExpiresAt);
        var (newRefreshToken, refreshExpiresAt) = await GenerateRefreshTokenAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (RefreshResult.Success, userInfo, new AuthTokens(accessToken, accessExpiresAt, newRefreshToken, refreshExpiresAt));
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = Hash(refreshToken);
        var stored = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private string GenerateAccessToken(IdentityUserInfo user, out DateTime expiresAt)
    {
        expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("display_name", user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Convert.FromBase64String(_jwtOptions.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<(string Token, DateTime ExpiresAt)> GenerateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);

        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (rawToken, expiresAt);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static IdentityUserInfo ToUserInfo(ApplicationUser user) =>
        new(user.Id, user.Email!, user.DisplayName, user.CreatedAt);
}
