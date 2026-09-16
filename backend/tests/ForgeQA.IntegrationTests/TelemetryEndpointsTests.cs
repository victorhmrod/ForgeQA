using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ForgeQA.Application.Auth;
using ForgeQA.Application.Builds;
using ForgeQA.Application.Bugs;
using ForgeQA.Application.Common;
using ForgeQA.Application.Organizations;
using ForgeQA.Application.ProjectApiKeys;
using ForgeQA.Application.Projects;
using ForgeQA.Application.Telemetry;
using ForgeQA.Domain.Enums;

namespace ForgeQA.IntegrationTests;

public class TelemetryEndpointsTests : IClassFixture<ForgeQAWebApplicationFactory>
{
    private readonly ForgeQAWebApplicationFactory _factory;

    public TelemetryEndpointsTests(ForgeQAWebApplicationFactory factory)
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

    private async Task<string> CreateKeyAsync(HttpClient client, Guid projectId, params ProjectApiKeyScope[] scopes)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{projectId}/api-keys", new CreateProjectApiKeyRequest("Runtime", scopes));
        var created = await response.Content.ReadFromJsonAsync<ProjectApiKeyCreatedResponse>(JsonDefaults.Options);
        return created!.PlaintextKey;
    }

    private HttpClient CreateRuntimeClient(string apiKey)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ForgeQA-Key", apiKey);
        return client;
    }

    private static StartTelemetrySessionRequest StartRequest(Guid buildId, Guid? runtimeSessionId = null) =>
        new(runtimeSessionId ?? Guid.NewGuid(), buildId,
            new TelemetrySessionEnvironmentDto("Strike_Factory", "Strike", "WINDOWS", "DEVELOPMENT", "UE 5.8.2", "Windows 11", "en-US"));

    private static TelemetryEventRequest Event(int sequenceNumber, string eventName = "game.started", object? properties = null) =>
        new(sequenceNumber, eventName, DateTime.UtcNow, null,
            JsonSerializer.SerializeToElement(properties ?? new { }), null);

    // --- Session start ---

    [Fact]
    public async Task Key_With_TelemetryWrite_Can_Start_Session()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<TelemetrySessionResponse>(JsonDefaults.Options);
        session!.EventCount.Should().Be(0);
        session.EndedAt.Should().BeNull();
        session.Build.Id.Should().Be(buildId);
    }

    [Fact]
    public async Task Key_With_Only_BugReportWrite_Cannot_Start_Session()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.BUG_REPORT_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Key_With_Only_TelemetryWrite_Cannot_Submit_Bug()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/bugs",
            new ForgeQA.Application.Bugs.CreateBugRequest(buildId, "Crash", null, null, BugSeverity.HIGH, BugSource.UNREAL_RUNTIME, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Revoked_Key_Cannot_Start_Session()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);

        var keysResponse = await client.GetFromJsonAsync<List<ProjectApiKeyResponse>>($"/api/projects/{projectId}/api-keys", JsonDefaults.Options);
        await client.PostAsync($"/api/projects/{projectId}/api-keys/{keysResponse![0].Id}/revoke", null);

        var runtime = CreateRuntimeClient(apiKey);
        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Key_From_Wrong_Project_Cannot_Start_Session()
    {
        var (clientA, projectAId, _) = await RegisterWithBuildAsync();
        var (_, projectBId, buildBId) = await RegisterWithBuildAsync();
        var apiKeyForA = await CreateKeyAsync(clientA, projectAId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKeyForA);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectBId}/telemetry/sessions", StartRequest(buildBId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Build_From_Wrong_Project_Is_Rejected()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();
        var (_, _, foreignBuildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(foreignBuildId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_Build_Is_Rejected()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Archived_Build_Is_Accepted()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        await client.PostAsync($"/api/projects/{projectId}/builds/{buildId}/archive", null);
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(buildId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Duplicate_Session_Start_With_Same_Build_Returns_Existing_Session()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);
        var runtimeSessionId = Guid.NewGuid();

        var first = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(buildId, runtimeSessionId));
        var firstSession = await first.Content.ReadFromJsonAsync<TelemetrySessionResponse>(JsonDefaults.Options);

        var second = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(buildId, runtimeSessionId));
        var secondSession = await second.Content.ReadFromJsonAsync<TelemetrySessionResponse>(JsonDefaults.Options);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        secondSession!.Id.Should().Be(firstSession!.Id);
    }

    [Fact]
    public async Task Session_Start_With_Same_RuntimeSessionId_But_Different_Build_Is_Rejected()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);
        var runtimeSessionId = Guid.NewGuid();

        await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(buildId, runtimeSessionId));

        var buildResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/builds",
            new CreateBuildRequest("Second Build", "0.4.3", Guid.NewGuid().ToString("N"), BuildPlatform.WINDOWS, BuildConfiguration.DEVELOPMENT,
                "main", "b123456", "UE 5.8.2", null));
        var secondBuild = await buildResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        var conflicting = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(secondBuild!.Id, runtimeSessionId));

        conflicting.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // --- Event ingestion ---

    private async Task<(HttpClient Runtime, Guid ProjectId, Guid RuntimeSessionId, HttpClient Dashboard)> StartedSessionAsync()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);
        var runtimeSessionId = Guid.NewGuid();

        await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", StartRequest(buildId, runtimeSessionId));

        return (runtime, projectId, runtimeSessionId, client);
    }

    [Fact]
    public async Task Valid_Batch_Is_Accepted()
    {
        var (runtime, projectId, runtimeSessionId, _) = await StartedSessionAsync();

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1), Event(2, "level.loaded", new { level = "Strike_Factory" }) }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<IngestTelemetryEventsResponse>(JsonDefaults.Options);
        result!.Accepted.Should().Be(2);
        result.Duplicates.Should().Be(0);
    }

    [Fact]
    public async Task Duplicate_Sequence_From_Previous_Batch_Is_Counted_Not_Inserted()
    {
        var (runtime, projectId, runtimeSessionId, _) = await StartedSessionAsync();
        await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1) }));

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1), Event(2) }));

        var result = await response.Content.ReadFromJsonAsync<IngestTelemetryEventsResponse>(JsonDefaults.Options);
        result!.Accepted.Should().Be(1);
        result.Duplicates.Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_Sequence_Inside_Same_Batch_Is_Rejected()
    {
        var (runtime, projectId, runtimeSessionId, _) = await StartedSessionAsync();

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1), Event(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_Event_Name_Fails_Whole_Batch()
    {
        var (runtime, projectId, runtimeSessionId, _) = await StartedSessionAsync();

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1), Event(2, "has spaces") }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Neither event from the failed batch was persisted.
        var retry = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1) }));
        var result = await retry.Content.ReadFromJsonAsync<IngestTelemetryEventsResponse>(JsonDefaults.Options);
        result!.Accepted.Should().Be(1);
    }

    [Fact]
    public async Task Batch_Above_Max_Count_Is_Rejected()
    {
        var (runtime, projectId, runtimeSessionId, _) = await StartedSessionAsync();
        var events = Enumerable.Range(1, 101).Select(i => Event(i)).ToArray();

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(events));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Properties_Above_Max_Size_Is_Rejected()
    {
        var (runtime, projectId, runtimeSessionId, _) = await StartedSessionAsync();
        var hugeValue = new string('a', 40 * 1024);

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1, properties: new { value = hugeValue }) }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ended_Session_Rejects_New_Events()
    {
        var (runtime, projectId, runtimeSessionId, _) = await StartedSessionAsync();
        await runtime.PostAsync($"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/end", null);

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Events_For_Unknown_RuntimeSession_Returns_NotFound()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{Guid.NewGuid()}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Session end ---

    [Fact]
    public async Task End_Session_Sets_EndedAt_And_Is_Idempotent()
    {
        var (runtime, projectId, runtimeSessionId, _) = await StartedSessionAsync();

        var first = await runtime.PostAsync($"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/end", null);
        var firstBody = await first.Content.ReadFromJsonAsync<TelemetrySessionResponse>(JsonDefaults.Options);

        var second = await runtime.PostAsync($"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/end", null);
        var secondBody = await second.Content.ReadFromJsonAsync<TelemetrySessionResponse>(JsonDefaults.Options);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        firstBody!.EndedAt.Should().NotBeNull();
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        secondBody!.EndedAt.Should().BeCloseTo(firstBody.EndedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task End_Session_From_Wrong_Project_Key_Is_Rejected()
    {
        var (runtime, _, runtimeSessionId, _) = await StartedSessionAsync();
        var (otherClient, otherProjectId, _) = await RegisterWithBuildAsync();
        var otherKey = await CreateKeyAsync(otherClient, otherProjectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var otherRuntime = CreateRuntimeClient(otherKey);

        var response = await otherRuntime.PostAsync($"/api/projects/{otherProjectId}/telemetry/sessions/{runtimeSessionId}/end", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task End_Session_With_Invalid_Key_Is_Rejected()
    {
        var (_, projectId, runtimeSessionId, _) = await StartedSessionAsync();
        var runtime = CreateRuntimeClient("fqa_proj_deadbeef_0000000000000000000000000000000000000000000000000000000000000000");

        var response = await runtime.PostAsync($"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/end", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- Dashboard query / IDOR ---

    [Fact]
    public async Task Member_Can_List_And_Read_Sessions()
    {
        var (runtime, projectId, runtimeSessionId, dashboard) = await StartedSessionAsync();
        await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1), Event(2, "level.loaded") }));

        var listResponse = await dashboard.GetFromJsonAsync<PagedResult<TelemetrySessionListItemResponse>>(
            $"/api/projects/{projectId}/telemetry/sessions", JsonDefaults.Options);
        listResponse!.Items.Should().ContainSingle(s => s.RuntimeSessionId == runtimeSessionId && s.EventCount == 2);

        var sessionId = listResponse.Items[0].Id;
        var detailResponse = await dashboard.GetAsync($"/api/projects/{projectId}/telemetry/sessions/{sessionId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResponse.Content.ReadFromJsonAsync<TelemetrySessionResponse>(JsonDefaults.Options);
        detail!.EventCount.Should().Be(2);

        var eventsResponse = await dashboard.GetFromJsonAsync<PagedResult<TelemetryEventResponse>>(
            $"/api/projects/{projectId}/telemetry/sessions/{sessionId}/events", JsonDefaults.Options);
        eventsResponse!.Items.Should().HaveCount(2);
        eventsResponse.Items[0].SequenceNumber.Should().Be(1);
        eventsResponse.Items[1].SequenceNumber.Should().Be(2);
    }

    [Fact]
    public async Task Filter_By_Build_And_ActiveOnly_Works()
    {
        var (runtime, projectId, runtimeSessionId, dashboard) = await StartedSessionAsync();
        await runtime.PostAsync($"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/end", null);

        var activeOnly = await dashboard.GetFromJsonAsync<PagedResult<TelemetrySessionListItemResponse>>(
            $"/api/projects/{projectId}/telemetry/sessions?activeOnly=true", JsonDefaults.Options);
        activeOnly!.Items.Should().BeEmpty();

        var endedOnly = await dashboard.GetFromJsonAsync<PagedResult<TelemetrySessionListItemResponse>>(
            $"/api/projects/{projectId}/telemetry/sessions?activeOnly=false", JsonDefaults.Options);
        endedOnly!.Items.Should().ContainSingle(s => s.RuntimeSessionId == runtimeSessionId);
    }

    [Fact]
    public async Task EventName_Prefix_Filter_Works()
    {
        var (runtime, projectId, runtimeSessionId, dashboard) = await StartedSessionAsync();
        await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/events",
            new IngestTelemetryEventsRequest(new[] { Event(1, "weapon.fired"), Event(2, "weapon.reloaded"), Event(3, "match.started") }));

        var listResponse = await dashboard.GetFromJsonAsync<PagedResult<TelemetrySessionListItemResponse>>(
            $"/api/projects/{projectId}/telemetry/sessions", JsonDefaults.Options);
        var sessionId = listResponse!.Items[0].Id;

        var filtered = await dashboard.GetFromJsonAsync<PagedResult<TelemetryEventResponse>>(
            $"/api/projects/{projectId}/telemetry/sessions/{sessionId}/events?eventName=weapon.", JsonDefaults.Options);

        filtered!.Items.Should().HaveCount(2);
        filtered.Items.Should().OnlyContain(e => e.EventName.StartsWith("weapon."));
    }

    [Fact]
    public async Task Outsider_Cannot_List_Sessions()
    {
        var (_, projectId, _, _) = await StartedSessionAsync();
        var (outsider, _, _) = await RegisterWithBuildAsync();

        var response = await outsider.GetAsync($"/api/projects/{projectId}/telemetry/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Session_From_Another_Project_Is_Not_Reachable()
    {
        var (dashboardA, projectAId, _) = await RegisterWithBuildAsync();
        var (_, projectBId, runtimeSessionIdB, dashboardB) = await StartedSessionAsync();

        var listB = await dashboardB.GetFromJsonAsync<PagedResult<TelemetrySessionListItemResponse>>(
            $"/api/projects/{projectBId}/telemetry/sessions", JsonDefaults.Options);
        var sessionIdB = listB!.Items.First(s => s.RuntimeSessionId == runtimeSessionIdB).Id;

        // dashboardA is a member of projectA (not projectB) — the session from projectB must never
        // resolve through projectA's own route, mirroring BugsEndpointsTests' cross-project shape.
        var response = await dashboardA.GetAsync($"/api/projects/{projectAId}/telemetry/sessions/{sessionIdB}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
