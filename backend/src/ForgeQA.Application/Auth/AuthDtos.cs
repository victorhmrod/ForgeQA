namespace ForgeQA.Application.Auth;

public record RegisterRequest(string Email, string DisplayName, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);

public record CurrentUserResponse(Guid Id, string Email, string DisplayName, DateTime CreatedAt);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    CurrentUserResponse User);
