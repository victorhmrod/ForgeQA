using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Auth;
using ForgeQA.Application.Builds;
using ForgeQA.Application.Bugs;
using ForgeQA.Application.Common;
using ForgeQA.Application.Organizations;
using ForgeQA.Application.ProjectApiKeys;
using ForgeQA.Application.Projects;
using ForgeQA.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeQA.IntegrationTests;

public class BugsEndpointsTests : IClassFixture<ForgeQAWebApplicationFactory>
{
    private readonly ForgeQAWebApplicationFactory _factory;

    public BugsEndpointsTests(ForgeQAWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid ProjectId, Guid BuildId)> RegisterWithBuildAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Victor", "SuperSecret123"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonDefaults.Options);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.AccessToken);

        var orgs = await client.GetFromJsonAsync<List<OrganizationResponse>>("/api/organizations", JsonDefaults.Options);
        var projectResponse = await client.PostAsJsonAsync($"/api/organizations/{orgs![0].Id}/projects", new CreateProjectRequest("FRONTLINE", null));
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonDefaults.Options);

        var buildResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project!.Id}/builds",
            new CreateBuildRequest("QA Candidate", "0.4.2", Guid.NewGuid().ToString("N"), BuildPlatform.WINDOWS, BuildConfiguration.DEVELOPMENT,
                "main", "a941de3", "UE 5.8.2", null));
        var build = await buildResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        return (client, project.Id, build!.Id);
    }

    private async Task<string> CreateRuntimeKeyAsync(HttpClient client, Guid projectId)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("Runtime", null));
        var created = await response.Content.ReadFromJsonAsync<ProjectApiKeyCreatedResponse>(JsonDefaults.Options);
        return created!.PlaintextKey;
    }

    private HttpClient CreateRuntimeClient(string apiKey)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ForgeQA-Key", apiKey);
        return client;
    }

    private static CreateBugRequest WebBugRequest(Guid? buildId = null, BugSeverity severity = BugSeverity.MEDIUM) =>
        new(buildId, "Weapon remains ADS after reload", "The rifle stays visually aimed after reloading.",
            "1. ADS\n2. Reload\n3. Release ADS", severity, BugSource.WEB, null, null, null);

    private static CreateBugRequest RuntimeBugRequest(Guid buildId, Guid? runtimeSessionId = null) =>
        new(buildId, "Crash on map load", "Client crashes loading Strike_Factory.", null, BugSeverity.CRITICAL, BugSource.UNREAL_RUNTIME,
            null, runtimeSessionId ?? Guid.NewGuid(),
            new BugEnvironmentDto("Strike_Factory", "Strike", "WINDOWS", "UE 5.8.2", "Windows 11", "Ryzen 9", "RTX 4080", 34_359_738_368, "en-US"));

    // --- Creation (JWT / WEB) ---

    [Fact]
    public async Task Member_Can_Create_Web_Bug_Without_Build()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bug = await response.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);
        bug!.Status.Should().Be(BugStatus.OPEN);
        bug.Source.Should().Be(BugSource.WEB);
        bug.Build.Should().BeNull();
        bug.Reporter.DisplayName.Should().Be("Victor");
    }

    [Fact]
    public async Task Web_Caller_Cannot_Submit_UnrealRuntime_Source()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/bugs",
            new CreateBugRequest(buildId, "Title", null, null, BugSeverity.LOW, BugSource.UNREAL_RUNTIME, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Outsider_Cannot_Create_Bug()
    {
        var (_, projectId, _) = await RegisterWithBuildAsync();
        var (outsiderClient, _, _) = await RegisterWithBuildAsync();

        var response = await outsiderClient.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Build correlation ---

    [Fact]
    public async Task Bug_Accepts_Valid_Build_In_Same_Project()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bug = await response.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);
        bug!.Build!.Id.Should().Be(buildId);
    }

    [Fact]
    public async Task Bug_Rejects_Build_From_Another_Project()
    {
        var (_, _, foreignBuildId) = await RegisterWithBuildAsync();
        var (client, projectId, _) = await RegisterWithBuildAsync();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(foreignBuildId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bug_Rejects_Unknown_Build()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bug_Accepts_Archived_Build()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        await client.PostAsync($"/api/projects/{projectId}/builds/{buildId}/archive", null);

        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // --- Cross-project IDOR ---

    [Fact]
    public async Task Cross_Project_Bug_Access_Returns_NotFound()
    {
        var (clientA, projectAId, _) = await RegisterWithBuildAsync();
        var (clientB, projectBId, _) = await RegisterWithBuildAsync();

        var createResponse = await clientB.PostAsJsonAsync($"/api/projects/{projectBId}/bugs", WebBugRequest());
        var bugInProjectB = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var response = await clientA.GetAsync($"/api/projects/{projectAId}/bugs/{bugInProjectB!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Outsider_Cannot_Read_Bug_Detail()
    {
        var (ownerClient, projectId, _) = await RegisterWithBuildAsync();
        var createResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest());
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var (outsiderClient, _, _) = await RegisterWithBuildAsync();
        var response = await outsiderClient.GetAsync($"/api/projects/{projectId}/bugs/{bug!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Listing ---

    [Fact]
    public async Task List_Bugs_Supports_Pagination_And_Includes_Build_Summary()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        for (var i = 0; i < 3; i++)
            await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));

        var response = await client.GetAsync($"/api/projects/{projectId}/bugs?page=1&pageSize=2");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BugListItemResponse>>(JsonDefaults.Options);

        page!.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(3);
        page.Items[0].Build!.Id.Should().Be(buildId);
    }

    [Fact]
    public async Task List_Bugs_Filters_By_Severity()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(severity: BugSeverity.LOW));
        await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(severity: BugSeverity.CRITICAL));

        var response = await client.GetAsync($"/api/projects/{projectId}/bugs?severity=CRITICAL");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BugListItemResponse>>(JsonDefaults.Options);

        page!.Items.Should().ContainSingle(b => b.Severity == BugSeverity.CRITICAL);
    }

    [Fact]
    public async Task List_Bugs_Searches_Title()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();
        await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest());

        var response = await client.GetAsync($"/api/projects/{projectId}/bugs?search=ADS");
        var page = await response.Content.ReadFromJsonAsync<PagedResult<BugListItemResponse>>(JsonDefaults.Options);

        page!.Items.Should().ContainSingle();
    }

    // --- Lifecycle ---

    [Fact]
    public async Task Bug_Lifecycle_Transitions_Set_Timestamps()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest());
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var toResolved = await client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}",
            new UpdateBugRequest(bug.Title, bug.Description, bug.ReproductionSteps, bug.Severity, BugStatus.RESOLVED));
        var resolved = await toResolved.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);
        resolved!.Status.Should().Be(BugStatus.RESOLVED);
        resolved.ResolvedAt.Should().NotBeNull();

        var toClosed = await client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug.Id}",
            new UpdateBugRequest(bug.Title, bug.Description, bug.ReproductionSteps, bug.Severity, BugStatus.CLOSED));
        var closed = await toClosed.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);
        closed!.Status.Should().Be(BugStatus.CLOSED);
        closed.ClosedAt.Should().NotBeNull();

        var reopened = await client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug.Id}",
            new UpdateBugRequest(bug.Title, bug.Description, bug.ReproductionSteps, bug.Severity, BugStatus.OPEN));
        var reopenedBody = await reopened.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);
        reopenedBody!.ResolvedAt.Should().BeNull();
        reopenedBody.ClosedAt.Should().BeNull();
    }

    [Fact]
    public async Task Update_Preserves_Captured_Environment()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var runtimeKey = await CreateRuntimeKeyAsync(client, projectId);
        var runtimeClient = CreateRuntimeClient(runtimeKey);

        var createResponse = await runtimeClient.PostAsJsonAsync($"/api/projects/{projectId}/bugs", RuntimeBugRequest(buildId));
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        await client.PatchAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}",
            new UpdateBugRequest("Updated title", "Updated", null, BugSeverity.HIGH, BugStatus.IN_PROGRESS));

        var detail = await client.GetFromJsonAsync<BugResponse>($"/api/projects/{projectId}/bugs/{bug.Id}", JsonDefaults.Options);
        detail!.Environment.MapName.Should().Be("Strike_Factory");
        detail.Environment.Gpu.Should().Be("RTX 4080");
    }

    // --- Runtime (Project API key) ---

    [Fact]
    public async Task Runtime_Key_Can_Create_Bug_In_Its_Own_Project()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateRuntimeKeyAsync(client, projectId);
        var runtimeClient = CreateRuntimeClient(apiKey);

        var response = await runtimeClient.PostAsJsonAsync($"/api/projects/{projectId}/bugs", RuntimeBugRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bug = await response.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);
        bug!.Source.Should().Be(BugSource.UNREAL_RUNTIME);
        bug.Reporter.Id.Should().BeNull();
    }

    [Fact]
    public async Task Runtime_Key_Cannot_Create_Bug_In_Another_Project()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateRuntimeKeyAsync(client, projectId);
        var runtimeClient = CreateRuntimeClient(apiKey);

        var (_, otherProjectId, otherBuildId) = await RegisterWithBuildAsync();

        var response = await runtimeClient.PostAsJsonAsync($"/api/projects/{otherProjectId}/bugs", RuntimeBugRequest(otherBuildId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Runtime_Key_Cannot_List_Bugs()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();
        var apiKey = await CreateRuntimeKeyAsync(client, projectId);
        var runtimeClient = CreateRuntimeClient(apiKey);

        var response = await runtimeClient.GetAsync($"/api/projects/{projectId}/bugs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Runtime_Key_Cannot_Update_Bugs()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var apiKey = await CreateRuntimeKeyAsync(client, projectId);
        var runtimeClient = CreateRuntimeClient(apiKey);

        var response = await runtimeClient.PatchAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}",
            new UpdateBugRequest("x", null, null, BugSeverity.LOW, BugStatus.CLOSED));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Revoked_Key_Cannot_Submit_Bug()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var keysResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("Runtime", null));
        var created = await keysResponse.Content.ReadFromJsonAsync<ProjectApiKeyCreatedResponse>(JsonDefaults.Options);

        await client.PostAsync($"/api/projects/{projectId}/api-keys/{created!.Id}/revoke", null);

        var runtimeClient = CreateRuntimeClient(created.PlaintextKey);
        var response = await runtimeClient.PostAsJsonAsync($"/api/projects/{projectId}/bugs", RuntimeBugRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_Key_Fails_Authentication()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var runtimeClient = CreateRuntimeClient("fqa_proj_deadbeef_0000000000000000000000000000000000000000000000000000000000000000");

        var response = await runtimeClient.PostAsJsonAsync($"/api/projects/{projectId}/bugs", RuntimeBugRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- Attachments ---

    [Fact]
    public async Task Attachment_Full_Lifecycle_Succeeds()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateRuntimeKeyAsync(client, projectId);
        var runtimeClient = CreateRuntimeClient(apiKey);

        var createResponse = await runtimeClient.PostAsJsonAsync($"/api/projects/{projectId}/bugs", RuntimeBugRequest(buildId));
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var initiateResponse = await runtimeClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}/attachments",
            new InitiateBugAttachmentRequest(BugAttachmentType.SCREENSHOT, "bug.png", "image/png", 2048));
        initiateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initiated = await initiateResponse.Content.ReadFromJsonAsync<InitiateBugAttachmentResponse>(JsonDefaults.Options);
        initiated!.UploadUrl.Should().StartWith("https://storage.test/put/");

        var storage = (FakeArtifactStorage)_factory.Services.GetRequiredService<IObjectStorage>();
        var objectKey = Uri.UnescapeDataString(initiated.UploadUrl.Replace("https://storage.test/put/", ""));
        storage.SimulateUpload(objectKey, 2048);

        var completeResponse = await runtimeClient.PostAsync(
            $"/api/projects/{projectId}/bugs/{bug.Id}/attachments/{initiated.AttachmentId}/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await completeResponse.Content.ReadFromJsonAsync<BugAttachmentResponse>(JsonDefaults.Options);
        completed!.Status.Should().Be(BugAttachmentStatus.READY);

        var downloadResponse = await client.PostAsync(
            $"/api/projects/{projectId}/bugs/{bug.Id}/attachments/{initiated.AttachmentId}/download", null);
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var download = await downloadResponse.Content.ReadFromJsonAsync<BugAttachmentDownloadResponse>(JsonDefaults.Options);
        download!.Url.Should().StartWith("https://storage.test/download/");
    }

    [Fact]
    public async Task Attachment_Completion_Fails_On_Size_Mismatch()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var initiateResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}/attachments",
            new InitiateBugAttachmentRequest(BugAttachmentType.SCREENSHOT, "bug.png", "image/png", 2048));
        var initiated = await initiateResponse.Content.ReadFromJsonAsync<InitiateBugAttachmentResponse>(JsonDefaults.Options);

        var storage = (FakeArtifactStorage)_factory.Services.GetRequiredService<IObjectStorage>();
        var objectKey = Uri.UnescapeDataString(initiated!.UploadUrl.Replace("https://storage.test/put/", ""));
        storage.SimulateUpload(objectKey, 999); // wrong size

        var completeResponse = await client.PostAsync(
            $"/api/projects/{projectId}/bugs/{bug.Id}/attachments/{initiated.AttachmentId}/complete", null);

        completeResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Attachment_Completion_Fails_When_Object_Missing()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var initiateResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}/attachments",
            new InitiateBugAttachmentRequest(BugAttachmentType.SCREENSHOT, "bug.png", "image/png", 2048));
        var initiated = await initiateResponse.Content.ReadFromJsonAsync<InitiateBugAttachmentResponse>(JsonDefaults.Options);

        // Never call SimulateUpload — the object never "arrives" in storage.
        var completeResponse = await client.PostAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}/attachments/{initiated!.AttachmentId}/complete", null);

        completeResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Wrong_Project_Cannot_Create_Attachment()
    {
        var (ownerClient, projectId, buildId) = await RegisterWithBuildAsync();
        var createResponse = await ownerClient.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var (outsiderClient, _, _) = await RegisterWithBuildAsync();
        var response = await outsiderClient.PostAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}/attachments",
            new InitiateBugAttachmentRequest(BugAttachmentType.SCREENSHOT, "bug.png", "image/png", 2048));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Wrong_Bug_Cannot_Complete_Attachment()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var bugAResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));
        var bugA = await bugAResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);
        var bugBResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));
        var bugB = await bugBResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var initiateResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bugA!.Id}/attachments",
            new InitiateBugAttachmentRequest(BugAttachmentType.SCREENSHOT, "bug.png", "image/png", 2048));
        var initiated = await initiateResponse.Content.ReadFromJsonAsync<InitiateBugAttachmentResponse>(JsonDefaults.Options);

        var response = await client.PostAsync(
            $"/api/projects/{projectId}/bugs/{bugB!.Id}/attachments/{initiated!.AttachmentId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Screenshot_Exceeding_Max_Size_Is_Rejected()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var createResponse = await client.PostAsJsonAsync($"/api/projects/{projectId}/bugs", WebBugRequest(buildId));
        var bug = await createResponse.Content.ReadFromJsonAsync<BugResponse>(JsonDefaults.Options);

        var response = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/bugs/{bug!.Id}/attachments",
            new InitiateBugAttachmentRequest(BugAttachmentType.SCREENSHOT, "huge.png", "image/png", 100L * 1024 * 1024));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
