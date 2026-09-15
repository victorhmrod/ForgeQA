using System.Net;
using System.Net.Http.Json;
using ForgeQA.Application.Auth;
using ForgeQA.Application.Organizations;
using ForgeQA.Application.Projects;
using FluentAssertions;

namespace ForgeQA.IntegrationTests;

public class ProjectsEndpointsTests : IClassFixture<ForgeQAWebApplicationFactory>
{
    private readonly ForgeQAWebApplicationFactory _factory;

    public ProjectsEndpointsTests(ForgeQAWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid OrganizationId)> RegisterAndGetClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Victor", "SuperSecret123"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.AccessToken);

        var orgsResponse = await client.GetAsync("/api/organizations");
        var orgs = await orgsResponse.Content.ReadFromJsonAsync<List<OrganizationResponse>>();

        return (client, orgs![0].Id);
    }

    [Fact]
    public async Task Create_Project_Succeeds_For_Organization_Member()
    {
        var (client, orgId) = await RegisterAndGetClientAsync();

        var response = await client.PostAsJsonAsync($"/api/organizations/{orgId}/projects", new CreateProjectRequest("FRONTLINE", "Competitive FPS"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        project!.Slug.Should().Be("frontline");
    }

    [Fact]
    public async Task List_Projects_Returns_Created_Project()
    {
        var (client, orgId) = await RegisterAndGetClientAsync();
        await client.PostAsJsonAsync($"/api/organizations/{orgId}/projects", new CreateProjectRequest("FRONTLINE", null));

        var response = await client.GetAsync($"/api/organizations/{orgId}/projects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>();
        projects.Should().ContainSingle(p => p.Name == "FRONTLINE");
    }

    [Fact]
    public async Task Get_Project_By_Id_Succeeds_For_Member()
    {
        var (client, orgId) = await RegisterAndGetClientAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/organizations/{orgId}/projects", new CreateProjectRequest("FRONTLINE", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>();

        var response = await client.GetAsync($"/api/projects/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Foreign_User_Cannot_Access_Another_Organizations_Project()
    {
        var (ownerClient, orgId) = await RegisterAndGetClientAsync();
        var createResponse = await ownerClient.PostAsJsonAsync($"/api/organizations/{orgId}/projects", new CreateProjectRequest("FRONTLINE", null));
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>();

        var (outsiderClient, _) = await RegisterAndGetClientAsync();

        var response = await outsiderClient.GetAsync($"/api/projects/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Foreign_User_Cannot_Create_Project_In_Another_Organization()
    {
        var (_, orgId) = await RegisterAndGetClientAsync();
        var (outsiderClient, _) = await RegisterAndGetClientAsync();

        var response = await outsiderClient.PostAsJsonAsync($"/api/organizations/{orgId}/projects", new CreateProjectRequest("FRONTLINE", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_User_Cannot_List_Projects()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid()}/projects");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
