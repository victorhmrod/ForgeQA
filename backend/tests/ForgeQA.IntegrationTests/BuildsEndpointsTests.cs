using System.Net;
using System.Net.Http.Json;
using ForgeQA.Application.Auth;
using ForgeQA.Application.Builds;
using ForgeQA.Application.Common;
using ForgeQA.Application.Organizations;
using ForgeQA.Application.Projects;
using ForgeQA.Domain.Enums;
using FluentAssertions;

namespace ForgeQA.IntegrationTests;

public class BuildsEndpointsTests : IClassFixture<ForgeQAWebApplicationFactory>
{
    private readonly ForgeQAWebApplicationFactory _factory;

    public BuildsEndpointsTests(ForgeQAWebApplicationFactory factory)
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

        var orgsResponse = await client.GetAsync("/api/organizations");
        var orgs = await orgsResponse.Content.ReadFromJsonAsync<List<OrganizationResponse>>();

        var projectResponse = await client.PostAsJsonAsync($"/api/organizations/{orgs![0].Id}/projects", new CreateProjectRequest("FRONTLINE", null));
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonDefaults.Options);

        return (client, project!.Id);
    }

    private static CreateBuildRequest ValidBuildRequest(
        string buildNumber = "1842",
        BuildPlatform platform = BuildPlatform.WINDOWS,
        BuildConfiguration configuration = BuildConfiguration.DEVELOPMENT,
        string version = "0.4.2") =>
        new("QA Candidate", version, buildNumber, platform, configuration, "main", "a941de3", "UE 5.8.2", "Inventory fixes.");

    // --- Creation ---

    [Fact]
    public async Task Create_Build_Succeeds_For_Project_Member()
    {
        var (client, projectId) = await RegisterWithProjectAsync();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var build = await response.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);
        build!.ProjectId.Should().Be(projectId);
        build.Version.Should().Be("0.4.2");
        build.BuildNumber.Should().Be("1842");
        build.Platform.Should().Be(BuildPlatform.WINDOWS);
        build.Configuration.Should().Be(BuildConfiguration.DEVELOPMENT);
        build.Source.Branch.Should().Be("main");
        build.Source.CommitSha.Should().Be("a941de3");
        build.CreatedBy.Name.Should().Be("Victor");
        build.ArchivedAt.Should().BeNull();
    }

    [Fact]
    public async Task Create_Build_Fails_When_Version_Is_Missing()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var payload = new { name = "X", version = "", buildNumber = "1", platform = "WINDOWS", configuration = "DEVELOPMENT" };

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Build_Fails_When_Platform_Is_Invalid()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var payload = new { version = "1.0.0", buildNumber = "1", platform = "NOT_A_PLATFORM", configuration = "DEVELOPMENT" };

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Build_Fails_When_Configuration_Is_Invalid()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var payload = new { version = "1.0.0", buildNumber = "1", platform = "WINDOWS", configuration = "NOT_A_CONFIG" };

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Build_Fails_When_Duplicate_Logical_Identity()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(version: "0.4.3"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Build_Succeeds_For_Same_BuildNumber_With_Different_Platform()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(platform: BuildPlatform.WINDOWS));

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(platform: BuildPlatform.LINUX));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_Build_Fails_When_Project_Does_Not_Exist()
    {
        var (client, _) = await RegisterWithProjectAsync();

        var response = await client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/builds", ValidBuildRequest());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Authorization ---

    [Fact]
    public async Task Outsider_Cannot_Create_Build()
    {
        var (_, projectId) = await RegisterWithProjectAsync();
        var (outsiderClient, _) = await RegisterWithProjectAsync();

        var response = await outsiderClient.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Outsider_Cannot_List_Builds()
    {
        var (_, projectId) = await RegisterWithProjectAsync();
        var (outsiderClient, _) = await RegisterWithProjectAsync();

        var response = await outsiderClient.GetAsync($"/api/projects/{projectId}/builds");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Outsider_Cannot_Get_Build()
    {
        var (ownerClient, projectId) = await RegisterWithProjectAsync();
        var createResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());
        var build = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        var (outsiderClient, _) = await RegisterWithProjectAsync();
        var response = await outsiderClient.GetAsync($"/api/projects/{projectId}/builds/{build!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Outsider_Cannot_Archive_Build()
    {
        var (ownerClient, projectId) = await RegisterWithProjectAsync();
        var createResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());
        var build = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        var (outsiderClient, _) = await RegisterWithProjectAsync();
        var response = await outsiderClient.PostAsync($"/api/projects/{projectId}/builds/{build!.Id}/archive", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cross_Project_Build_Access_Returns_NotFound()
    {
        var (clientA, projectAId) = await RegisterWithProjectAsync();
        var (clientB, projectBId) = await RegisterWithProjectAsync();

        var createResponse = await clientB.PostAsJsonAsync($"/api/projects/{projectBId}/builds", ValidBuildRequest());
        var buildInProjectB = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        // clientA has access to projectA, but the build belongs to projectB.
        var response = await clientA.GetAsync($"/api/projects/{projectAId}/builds/{buildInProjectB!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Listing, pagination, filters, search ---

    [Fact]
    public async Task List_Builds_Supports_Pagination()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        for (var i = 1; i <= 5; i++)
            await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: $"build-{i}"));

        var response = await client.GetAsync($"/api/projects/{projectId}/builds?page=1&pageSize=2");
        var page1 = await response.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);

        page1!.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
        page1.TotalPages.Should().Be(3);

        var response2 = await client.GetAsync($"/api/projects/{projectId}/builds?page=2&pageSize=2");
        var page2 = await response2.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);
        page2!.Items.Should().HaveCount(2);

        page1.Items.Select(b => b.Id).Should().NotIntersectWith(page2.Items.Select(b => b.Id));
    }

    [Fact]
    public async Task List_Builds_Default_Sort_Is_CreatedAt_Descending()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: "1"));
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: "2"));

        var response = await client.GetAsync($"/api/projects/{projectId}/builds");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);

        page!.Items[0].BuildNumber.Should().Be("2");
        page.Items[1].BuildNumber.Should().Be("1");
    }

    [Fact]
    public async Task List_Builds_Filters_By_Platform()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: "1", platform: BuildPlatform.WINDOWS));
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: "2", platform: BuildPlatform.LINUX));

        var response = await client.GetAsync($"/api/projects/{projectId}/builds?platform=LINUX");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);

        page!.Items.Should().ContainSingle();
        page.Items[0].Platform.Should().Be(BuildPlatform.LINUX);
    }

    [Fact]
    public async Task List_Builds_Filters_By_Configuration()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: "1", configuration: BuildConfiguration.DEVELOPMENT));
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: "2", configuration: BuildConfiguration.SHIPPING));

        var response = await client.GetAsync($"/api/projects/{projectId}/builds?configuration=SHIPPING");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);

        page!.Items.Should().ContainSingle();
        page.Items[0].Configuration.Should().Be(BuildConfiguration.SHIPPING);
    }

    [Fact]
    public async Task List_Builds_Searches_Across_Fields()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: "jenkins-891"));
        await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest(buildNumber: "gha-3421", platform: BuildPlatform.LINUX));

        var response = await client.GetAsync($"/api/projects/{projectId}/builds?search=jenkins");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);

        page!.Items.Should().ContainSingle(b => b.BuildNumber == "jenkins-891");
    }

    // --- Update ---

    [Fact]
    public async Task Update_Build_Changes_Editable_Metadata()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());
        var build = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        var update = new UpdateBuildRequest("0.4.3", "Renamed Candidate", "release", "b123456", "UE 5.8.3", "Updated notes.");
        var response = await client.PatchAsJsonAsync($"/api/projects/{projectId}/builds/{build!.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);
        updated!.Version.Should().Be("0.4.3");
        updated.Name.Should().Be("Renamed Candidate");
        updated.Source.Branch.Should().Be("release");

        // Identity fields are immutable via PATCH.
        updated.BuildNumber.Should().Be(build.BuildNumber);
        updated.Platform.Should().Be(build.Platform);
        updated.Configuration.Should().Be(build.Configuration);
    }

    [Fact]
    public async Task Update_Build_Fails_With_Invalid_Version()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());
        var build = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        var update = new UpdateBuildRequest("", null, null, null, null, null);
        var response = await client.PatchAsJsonAsync($"/api/projects/{projectId}/builds/{build!.Id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --- Archive / restore ---

    [Fact]
    public async Task Build_Can_Be_Archived_And_Disappears_From_Default_Listing()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());
        var build = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        var archiveResponse = await client.PostAsync($"/api/projects/{projectId}/builds/{build!.Id}/archive", null);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var archived = await archiveResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);
        archived!.ArchivedAt.Should().NotBeNull();

        var listResponse = await client.GetAsync($"/api/projects/{projectId}/builds");
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);
        page!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Archived_Build_Can_Still_Be_Retrieved_Directly()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());
        var build = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);
        await client.PostAsync($"/api/projects/{projectId}/builds/{build!.Id}/archive", null);

        var response = await client.GetAsync($"/api/projects/{projectId}/builds/{build.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Archived_Build_Appears_When_Explicitly_Filtered()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());
        var build = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);
        await client.PostAsync($"/api/projects/{projectId}/builds/{build!.Id}/archive", null);

        var response = await client.GetAsync($"/api/projects/{projectId}/builds?status=archived");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);

        page!.Items.Should().ContainSingle(b => b.Id == build.Id);
    }

    [Fact]
    public async Task Build_Can_Be_Restored()
    {
        var (client, projectId) = await RegisterWithProjectAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/builds", ValidBuildRequest());
        var build = await createResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);
        await client.PostAsync($"/api/projects/{projectId}/builds/{build!.Id}/archive", null);

        var restoreResponse = await client.PostAsync($"/api/projects/{projectId}/builds/{build.Id}/restore", null);

        restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = await restoreResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);
        restored!.ArchivedAt.Should().BeNull();

        var listResponse = await client.GetAsync($"/api/projects/{projectId}/builds");
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<BuildResponse>>(JsonDefaults.Options);
        page!.Items.Should().ContainSingle(b => b.Id == build.Id);
    }
}
