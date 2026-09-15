using System.Net;
using System.Net.Http.Json;
using ForgeQA.Application.Auth;
using FluentAssertions;

namespace ForgeQA.IntegrationTests;

public class AuthEndpointsTests : IClassFixture<ForgeQAWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsTests(ForgeQAWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Creates_User_With_Personal_Organization_As_Owner()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest(email, "Victor", "SuperSecret123");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.User.Email.Should().Be(email);
        body.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();

        _client.DefaultRequestHeaders.Authorization = new("Bearer", body.AccessToken);
        var orgsResponse = await _client.GetAsync("/api/organizations");
        var orgs = await orgsResponse.Content.ReadFromJsonAsync<List<ForgeQA.Application.Organizations.OrganizationResponse>>();

        orgs.Should().ContainSingle();
        orgs![0].Role.Should().Be("Owner");
        orgs[0].Name.Should().Be("Victor's Workspace");
    }

    [Fact]
    public async Task Register_Fails_When_Email_Already_Registered()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest(email, "Victor", "SuperSecret123");
        await _client.PostAsJsonAsync("/api/auth/register", request);

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_Succeeds_With_Valid_Credentials()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Victor", "SuperSecret123"));

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "SuperSecret123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_Fails_With_Invalid_Credentials()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("unknown@example.com", "wrong"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_Requires_Authentication()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_Issues_New_Tokens()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Victor", "SuperSecret123"));
        var registered = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(registered!.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>();
        refreshed!.RefreshToken.Should().NotBe(registered.RefreshToken);
    }
}
