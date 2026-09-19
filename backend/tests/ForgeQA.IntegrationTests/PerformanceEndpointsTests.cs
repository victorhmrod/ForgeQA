using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ForgeQA.Application.Auth;
using ForgeQA.Application.Builds;
using ForgeQA.Application.Bugs;
using ForgeQA.Application.Common;
using ForgeQA.Application.Organizations;
using ForgeQA.Application.Performance;
using ForgeQA.Application.ProjectApiKeys;
using ForgeQA.Application.Projects;
using ForgeQA.Application.Telemetry;
using ForgeQA.Domain.Enums;

namespace ForgeQA.IntegrationTests;

public class PerformanceEndpointsTests : IClassFixture<ForgeQAWebApplicationFactory>
{
    private readonly ForgeQAWebApplicationFactory _factory;

    public PerformanceEndpointsTests(ForgeQAWebApplicationFactory factory)
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
            new CreateBuildRequest("QA Candidate", "0.6.0", Guid.NewGuid().ToString("N"), BuildPlatform.WINDOWS, BuildConfiguration.DEVELOPMENT,
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

    private async Task<(HttpClient Telemetry, HttpClient Performance, Guid ProjectId, Guid RuntimeSessionId, HttpClient Dashboard)> StartedTelemetrySessionAsync()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var telemetryKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var performanceKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.PERFORMANCE_WRITE);
        var telemetryClient = CreateRuntimeClient(telemetryKey);
        var performanceClient = CreateRuntimeClient(performanceKey);
        var runtimeSessionId = Guid.NewGuid();

        await telemetryClient.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions",
            new StartTelemetrySessionRequest(runtimeSessionId, buildId, new TelemetrySessionEnvironmentDto("Strike_Factory", "Strike", "WINDOWS", "DEVELOPMENT", "UE 5.8.2", "Windows 11", "en-US")));

        return (telemetryClient, performanceClient, projectId, runtimeSessionId, client);
    }

    private static PerformanceSampleRequest Sample(
        int sequenceNumber, double fps = 60.0, double frameTimeMs = 16.67, string? mapName = null,
        double? gameThreadTimeMs = null, long? memoryUsedBytes = null) =>
        new(sequenceNumber, DateTime.UtcNow, mapName, fps, frameTimeMs, gameThreadTimeMs, null, null, memoryUsedBytes, null, null, null, null, null, null);

    // --- Scope enforcement / isolation ---

    [Fact]
    public async Task Key_With_PerformanceWrite_Can_Ingest_Samples()
    {
        var (_, performance, projectId, runtimeSessionId, _) = await StartedTelemetrySessionAsync();

        var response = await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1), Sample(2) }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<IngestPerformanceSamplesResponse>(JsonDefaults.Options);
        result!.Accepted.Should().Be(2);
    }

    [Fact]
    public async Task Key_With_Only_BugReportWrite_Cannot_Ingest_Performance()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.BUG_REPORT_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{Guid.NewGuid()}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Key_With_Only_TelemetryWrite_Cannot_Ingest_Performance()
    {
        var (telemetry, _, projectId, runtimeSessionId, _) = await StartedTelemetrySessionAsync();

        var response = await telemetry.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Key_With_Only_PerformanceWrite_Cannot_Submit_Bug()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.PERFORMANCE_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/bugs",
            new CreateBugRequest(buildId, "Crash", null, null, BugSeverity.HIGH, BugSource.UNREAL_RUNTIME, null, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Key_With_Only_PerformanceWrite_Cannot_Ingest_Telemetry_Events()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.PERFORMANCE_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions",
            new StartTelemetrySessionRequest(Guid.NewGuid(), buildId, null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Revoked_Key_Cannot_Ingest_Performance()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.PERFORMANCE_WRITE);
        var keysResponse = await client.GetFromJsonAsync<List<ProjectApiKeyResponse>>($"/api/projects/{projectId}/api-keys", JsonDefaults.Options);
        await client.PostAsync($"/api/projects/{projectId}/api-keys/{keysResponse![0].Id}/revoke", null);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{Guid.NewGuid()}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Key_From_Wrong_Project_Cannot_Ingest_Performance()
    {
        var (_, performanceA, projectAId, _, _) = await StartedTelemetrySessionAsync();
        var (_, _, projectBId, runtimeSessionIdB, _) = await StartedTelemetrySessionAsync();

        var response = await performanceA.PostAsJsonAsync(
            $"/api/projects/{projectBId}/performance/sessions/{runtimeSessionIdB}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Session relationship ---

    [Fact]
    public async Task Ingestion_Before_Telemetry_Session_Exists_Is_Rejected()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();
        var apiKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.PERFORMANCE_WRITE);
        var runtime = CreateRuntimeClient(apiKey);

        var response = await runtime.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{Guid.NewGuid()}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ended_Telemetry_Session_Rejects_New_Performance_Samples()
    {
        var (telemetry, performance, projectId, runtimeSessionId, _) = await StartedTelemetrySessionAsync();
        await telemetry.PostAsync($"/api/projects/{projectId}/telemetry/sessions/{runtimeSessionId}/end", null);

        var response = await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // --- Batch validation ---

    [Fact]
    public async Task Duplicate_Sequence_From_Previous_Batch_Is_Counted_Not_Inserted()
    {
        var (_, performance, projectId, runtimeSessionId, _) = await StartedTelemetrySessionAsync();
        await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));

        var response = await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1), Sample(2) }));

        var result = await response.Content.ReadFromJsonAsync<IngestPerformanceSamplesResponse>(JsonDefaults.Options);
        result!.Accepted.Should().Be(1);
        result.Duplicates.Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_Sequence_Inside_Same_Batch_Is_Rejected()
    {
        var (_, performance, projectId, runtimeSessionId, _) = await StartedTelemetrySessionAsync();

        var response = await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1), Sample(1) }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Batch_Above_Max_Count_Is_Rejected()
    {
        var (_, performance, projectId, runtimeSessionId, _) = await StartedTelemetrySessionAsync();
        var samples = Enumerable.Range(1, 121).Select(i => Sample(i)).ToArray();

        var response = await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(samples));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(-1.0, 16.67)]
    [InlineData(60.0, -1.0)]
    public async Task Invalid_Metrics_Are_Rejected_Atomically(double fps, double frameTimeMs)
    {
        var (_, performance, projectId, runtimeSessionId, _) = await StartedTelemetrySessionAsync();

        var response = await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1), Sample(2, fps: fps, frameTimeMs: frameTimeMs) }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var retry = await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));
        var result = await retry.Content.ReadFromJsonAsync<IngestPerformanceSamplesResponse>(JsonDefaults.Options);
        result!.Accepted.Should().Be(1, "nothing from the failed batch should have been persisted");
    }

    // --- Session summary / percentiles ---

    [Fact]
    public async Task Session_Summary_Computes_Aggregates_And_Percentiles()
    {
        var (_, performance, projectId, runtimeSessionId, dashboard) = await StartedTelemetrySessionAsync();

        // 100 samples: fps 1..100, frameTimeMs 1..100 — deterministic fixture for percentile assertions.
        var samples = Enumerable.Range(1, 100).Select(i => Sample(i, fps: i, frameTimeMs: i, memoryUsedBytes: 1000 + i)).ToArray();
        await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(samples));

        var listResponse = await dashboard.GetFromJsonAsync<PagedResult<PerformanceSessionListItemResponse>>(
            $"/api/projects/{projectId}/performance/sessions", JsonDefaults.Options);
        var sessionId = listResponse!.Items.Single(s => s.RuntimeSessionId == runtimeSessionId).SessionId;

        var summaryResponse = await dashboard.GetAsync($"/api/projects/{projectId}/performance/sessions/{sessionId}");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<PerformanceSessionSummaryResponse>(JsonDefaults.Options);

        summary!.Summary.SampleCount.Should().Be(100);
        summary.Summary.AverageFps.Should().BeApproximately(50.5, 0.01);
        summary.Summary.MinimumFps.Should().Be(1);
        summary.Summary.MaximumFrameTimeMs.Should().Be(100);
        summary.Summary.P50FrameTimeMs.Should().BeApproximately(50.5, 0.01);
        summary.Summary.P95FrameTimeMs.Should().BeApproximately(95.05, 0.5);
        summary.Summary.PeakMemoryUsedBytes.Should().Be(1100);
    }

    [Fact]
    public async Task Paginated_Samples_Are_Ordered_By_Sequence()
    {
        var (_, performance, projectId, runtimeSessionId, dashboard) = await StartedTelemetrySessionAsync();
        await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1), Sample(2), Sample(3) }));

        var listResponse = await dashboard.GetFromJsonAsync<PagedResult<PerformanceSessionListItemResponse>>(
            $"/api/projects/{projectId}/performance/sessions", JsonDefaults.Options);
        var sessionId = listResponse!.Items.Single().SessionId;

        var samplesResponse = await dashboard.GetFromJsonAsync<PagedResult<PerformanceSampleResponse>>(
            $"/api/projects/{projectId}/performance/sessions/{sessionId}/samples", JsonDefaults.Options);

        samplesResponse!.Items.Should().HaveCount(3);
        samplesResponse.Items.Select(s => s.SequenceNumber).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task Series_Endpoint_Downsamples_Large_Sessions()
    {
        var (_, performance, projectId, runtimeSessionId, dashboard) = await StartedTelemetrySessionAsync();
        var samples = Enumerable.Range(1, 100).Select(i => Sample(i, fps: i)).ToArray();
        await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(samples));

        var listResponse = await dashboard.GetFromJsonAsync<PagedResult<PerformanceSessionListItemResponse>>(
            $"/api/projects/{projectId}/performance/sessions", JsonDefaults.Options);
        var sessionId = listResponse!.Items.Single().SessionId;

        var series = await dashboard.GetFromJsonAsync<PerformanceSeriesResponse>(
            $"/api/projects/{projectId}/performance/sessions/{sessionId}/series?maxPoints=10", JsonDefaults.Options);

        series!.Points.Should().HaveCount(10);
        series.Points.Sum(p => 1).Should().Be(10);
    }

    [Fact]
    public async Task Map_Breakdown_Groups_By_MapName()
    {
        var (_, performance, projectId, runtimeSessionId, dashboard) = await StartedTelemetrySessionAsync();
        await performance.PostAsJsonAsync(
            $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
            new IngestPerformanceSamplesRequest(new[]
            {
                Sample(1, fps: 70, mapName: "Strike_Factory"),
                Sample(2, fps: 80, mapName: "Strike_Factory"),
                Sample(3, fps: 120, mapName: "Lobby"),
            }));

        var listResponse = await dashboard.GetFromJsonAsync<PagedResult<PerformanceSessionListItemResponse>>(
            $"/api/projects/{projectId}/performance/sessions", JsonDefaults.Options);
        var sessionId = listResponse!.Items.Single().SessionId;

        var breakdown = await dashboard.GetFromJsonAsync<List<ForgeQA.Application.Abstractions.MapBreakdownItem>>(
            $"/api/projects/{projectId}/performance/sessions/{sessionId}/maps", JsonDefaults.Options);

        breakdown!.Should().Contain(m => m.MapName == "Strike_Factory" && m.SampleCount == 2);
        breakdown.Should().Contain(m => m.MapName == "Lobby" && m.SampleCount == 1);
    }

    // --- Build aggregation / comparison ---

    [Fact]
    public async Task Build_Summary_Aggregates_Across_Sessions()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var telemetryKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var performanceKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.PERFORMANCE_WRITE);
        var telemetry = CreateRuntimeClient(telemetryKey);
        var performance = CreateRuntimeClient(performanceKey);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var runtimeSessionId = Guid.NewGuid();
            await telemetry.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", new StartTelemetrySessionRequest(runtimeSessionId, buildId, null));
            await performance.PostAsJsonAsync(
                $"/api/projects/{projectId}/performance/sessions/{runtimeSessionId}/samples",
                new IngestPerformanceSamplesRequest(new[] { Sample(1, fps: 90), Sample(2, fps: 100) }));
        }

        var buildsResponse = await client.GetFromJsonAsync<List<BuildPerformanceListItemResponse>>($"/api/projects/{projectId}/performance/builds", JsonDefaults.Options);

        var summary = buildsResponse!.Single(b => b.BuildId == buildId);
        summary.SessionCount.Should().Be(2);
        summary.Summary.SampleCount.Should().Be(4);
        summary.Summary.AverageFps.Should().BeApproximately(95.0, 0.01);
    }

    [Fact]
    public async Task Builds_With_No_Performance_Samples_Are_Omitted()
    {
        var (client, projectId, _) = await RegisterWithBuildAsync();

        var buildsResponse = await client.GetFromJsonAsync<List<BuildPerformanceListItemResponse>>($"/api/projects/{projectId}/performance/builds", JsonDefaults.Options);

        buildsResponse.Should().BeEmpty();
    }

    [Fact]
    public async Task Compare_Returns_Deltas_Between_Two_Builds()
    {
        var (client, projectId, buildAId) = await RegisterWithBuildAsync();
        var buildBResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/builds",
            new CreateBuildRequest("Second", "0.6.1", Guid.NewGuid().ToString("N"), BuildPlatform.WINDOWS, BuildConfiguration.DEVELOPMENT, "main", "abc123", "UE 5.8.2", null));
        var buildB = await buildBResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        var telemetryKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.TELEMETRY_WRITE);
        var performanceKey = await CreateKeyAsync(client, projectId, ProjectApiKeyScope.PERFORMANCE_WRITE);
        var telemetry = CreateRuntimeClient(telemetryKey);
        var performance = CreateRuntimeClient(performanceKey);

        var sessionA = Guid.NewGuid();
        await telemetry.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", new StartTelemetrySessionRequest(sessionA, buildAId, null));
        await performance.PostAsJsonAsync($"/api/projects/{projectId}/performance/sessions/{sessionA}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1, fps: 60, frameTimeMs: 16.0) }));

        var sessionB = Guid.NewGuid();
        await telemetry.PostAsJsonAsync($"/api/projects/{projectId}/telemetry/sessions", new StartTelemetrySessionRequest(sessionB, buildB!.Id, null));
        await performance.PostAsJsonAsync($"/api/projects/{projectId}/performance/sessions/{sessionB}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1, fps: 90, frameTimeMs: 12.0) }));

        var compareResponse = await client.GetAsync($"/api/projects/{projectId}/performance/compare?buildA={buildAId}&buildB={buildB.Id}");
        compareResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var compare = await compareResponse.Content.ReadFromJsonAsync<PerformanceCompareResponse>(JsonDefaults.Options);

        var fpsMetric = compare!.Metrics.Single(m => m.Metric == "AverageFps");
        fpsMetric.BuildAValue.Should().Be(60);
        fpsMetric.BuildBValue.Should().Be(90);
        fpsMetric.Delta.Should().Be(30);
        fpsMetric.DeltaPercent.Should().BeApproximately(50.0, 0.01);
    }

    [Fact]
    public async Task Compare_With_Foreign_Build_Is_Rejected()
    {
        var (client, projectId, buildAId) = await RegisterWithBuildAsync();
        var (_, _, foreignBuildId) = await RegisterWithBuildAsync();

        var response = await client.GetAsync($"/api/projects/{projectId}/performance/compare?buildA={buildAId}&buildB={foreignBuildId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- IDOR ---

    [Fact]
    public async Task Outsider_Cannot_List_Performance_Sessions()
    {
        var (_, _, projectId, _, _) = await StartedTelemetrySessionAsync();
        var (outsider, _, _) = await RegisterWithBuildAsync();

        var response = await outsider.GetAsync($"/api/projects/{projectId}/performance/sessions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Session_From_Another_Project_Is_Not_Reachable()
    {
        var (dashboardA, projectAId, _) = await RegisterWithBuildAsync();
        var (_, performanceB, projectBId, runtimeSessionIdB, dashboardB) = await StartedTelemetrySessionAsync();
        await performanceB.PostAsJsonAsync(
            $"/api/projects/{projectBId}/performance/sessions/{runtimeSessionIdB}/samples",
            new IngestPerformanceSamplesRequest(new[] { Sample(1) }));

        var listB = await dashboardB.GetFromJsonAsync<PagedResult<PerformanceSessionListItemResponse>>(
            $"/api/projects/{projectBId}/performance/sessions", JsonDefaults.Options);
        var sessionIdB = listB!.Items.Single().SessionId;

        var response = await dashboardA.GetAsync($"/api/projects/{projectAId}/performance/sessions/{sessionIdB}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
