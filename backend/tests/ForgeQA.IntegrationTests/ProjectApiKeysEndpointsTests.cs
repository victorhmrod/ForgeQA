using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ForgeQA.Application.Auth;
using ForgeQA.Application.Organizations;
using ForgeQA.Application.ProjectApiKeys;
using ForgeQA.Application.Projects;
using ForgeQA.Domain.Enums;

namespace ForgeQA.IntegrationTests;

public class ProjectApiKeysEndpointsTests : IClassFixture<ForgeQAWebApplicationFactory>
{
    private readonly ForgeQAWebApplicationFactory _factory;

    public ProjectApiKeysEndpointsTests(ForgeQAWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid ProjectId)> RegisterWithProjectAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Victor", "SuperSecret123"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonDefaults.Options);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.AccessToken);

        var orgs = await client.GetFromJsonAsync<List<OrganizationResponse>>("/api/organizations", JsonDefaults.Options);
        var projectResponse = await client.PostAsJsonAsync($"/api/organizations/{orgs![0].Id}/projects", new CreateProjectRequest("FRONTLINE", null));
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonDefaults.Options);

        return (client, project!.Id);
    }

    [Fact]
    public async Task Create_Key_Returns_Plaintext_Secret_Once()
    {
        var (client, projectId) = await RegisterWithProjectAsync();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("CI Runner", null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ProjectApiKeyCreatedResponse>(JsonDefaults.Options);
        created!.PlaintextKey.Should().StartWith("fqa_proj_");
        created.Scopes.Should().Contain(ProjectApiKeyScope.BUG_REPORT_WRITE);
    }

    [Fact]
    public async Task Create_Key_Response_Body_Never_Contains_A_Hash_Field()
    {
        var (client, projectId) = await RegisterWithProjectAsync();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("CI Runner", null));
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain("keyHash", "the plaintext response DTO must never expose the stored hash");
    }

    [Fact]
    public async Task List_Keys_Does_Not_Include_Plaintext()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("CI Runner", null));

        var response = await client.GetAsync($"/api/projects/{projectId}/api-keys");
        var raw = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        raw.Should().NotContain("fqa_proj_", "listing keys must never reveal a plaintext secret");
        raw.Should().NotContain("keyHash");
    }

    [Fact]
    public async Task Revoke_Sets_RevokedAt()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("CI Runner", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectApiKeyCreatedResponse>(JsonDefaults.Options);

        var response = await client.PostAsync($"/api/projects/{projectId}/api-keys/{created!.Id}/revoke", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var revoked = await response.Content.ReadFromJsonAsync<ProjectApiKeyResponse>(JsonDefaults.Options);
        revoked!.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Outsider_Cannot_Create_Key()
    {
        var (_, projectId) = await RegisterWithProjectAsync();
        var (outsiderClient, _) = await RegisterWithProjectAsync();

        var response = await outsiderClient.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("Evil", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Outsider_Cannot_List_Keys()
    {
        var (_, projectId) = await RegisterWithProjectAsync();
        var (outsiderClient, _) = await RegisterWithProjectAsync();

        var response = await outsiderClient.GetAsync($"/api/projects/{projectId}/api-keys");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Outsider_Cannot_Revoke_Key()
    {
        var (ownerClient, projectId) = await RegisterWithProjectAsync();
        var createResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("CI Runner", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectApiKeyCreatedResponse>(JsonDefaults.Options);

        var (outsiderClient, _) = await RegisterWithProjectAsync();
        var response = await outsiderClient.PostAsync($"/api/projects/{projectId}/api-keys/{created!.Id}/revoke", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
