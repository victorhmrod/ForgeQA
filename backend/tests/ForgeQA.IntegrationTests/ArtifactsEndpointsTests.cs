using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ForgeQA.Application.Artifacts;
using ForgeQA.Application.Auth;
using ForgeQA.Application.Builds;
using ForgeQA.Application.Organizations;
using ForgeQA.Application.Projects;
using ForgeQA.Domain.Enums;

namespace ForgeQA.IntegrationTests;

public class ArtifactsEndpointsTests : IClassFixture<ForgeQAWebApplicationFactory>
{
    private readonly ForgeQAWebApplicationFactory _factory;

    public ArtifactsEndpointsTests(ForgeQAWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Member_Can_Initiate_List_Complete_And_Download_Artifact()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();

        var initiate = await InitiateAsync(client, projectId, buildId);
        initiate.StatusCode.Should().Be(HttpStatusCode.OK);
        var upload = await initiate.Content.ReadFromJsonAsync<InitiateArtifactUploadResponse>(JsonDefaults.Options);
        upload.Should().NotBeNull();
        upload!.PartCount.Should().Be(1);

        var list = await client.GetFromJsonAsync<List<BuildArtifactResponse>>(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts",
            JsonDefaults.Options);
        list.Should().ContainSingle();
        list![0].Status.Should().Be(ArtifactStatus.UPLOADING);

        var urlsResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts/{upload.ArtifactId}/upload-parts",
            new RequestUploadPartsRequest(upload.UploadSessionId, new[] { 1 }),
            JsonDefaults.Options);
        urlsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var completeResponse = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts/{upload.ArtifactId}/complete",
            new CompleteArtifactUploadRequest(upload.UploadSessionId, new[] { new CompletedUploadPartRequest(1, "etag-1") }),
            JsonDefaults.Options);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = await completeResponse.Content.ReadFromJsonAsync<BuildArtifactResponse>(JsonDefaults.Options);
        completed!.Status.Should().Be(ArtifactStatus.READY);

        var downloadResponse = await client.PostAsync(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts/{upload.ArtifactId}/download",
            null);
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var download = await downloadResponse.Content.ReadFromJsonAsync<ArtifactDownloadResponse>(JsonDefaults.Options);
        download!.Url.Should().StartWith("https://storage.test/download/");
    }

    [Fact]
    public async Task Outsider_Cannot_Initiate_Artifact_Upload()
    {
        var (_, projectId, buildId) = await RegisterWithBuildAsync();
        var (outsider, _, _) = await RegisterWithBuildAsync();

        var response = await InitiateAsync(outsider, projectId, buildId);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cross_Project_Artifact_Id_Returns_NotFound()
    {
        var (clientA, projectA, buildA) = await RegisterWithBuildAsync();
        var (clientB, projectB, buildB) = await RegisterWithBuildAsync();
        var created = await InitiateAsync(clientB, projectB, buildB);
        var artifact = await created.Content.ReadFromJsonAsync<InitiateArtifactUploadResponse>(JsonDefaults.Options);

        var response = await clientA.GetAsync(
            $"/api/projects/{projectA}/builds/{buildA}/artifacts/{artifact!.ArtifactId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Archived_Build_Rejects_New_Artifact_Uploads()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var archive = await client.PostAsync($"/api/projects/{projectId}/builds/{buildId}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await InitiateAsync(client, projectId, buildId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Upload_Can_Be_Aborted_And_Cannot_Be_Downloaded()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var created = await InitiateAsync(client, projectId, buildId);
        var upload = await created.Content.ReadFromJsonAsync<InitiateArtifactUploadResponse>(JsonDefaults.Options);

        var abort = await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts/{upload!.ArtifactId}/abort",
            new AbortArtifactUploadRequest(upload.UploadSessionId),
            JsonDefaults.Options);
        abort.StatusCode.Should().Be(HttpStatusCode.OK);
        var artifact = await abort.Content.ReadFromJsonAsync<BuildArtifactResponse>(JsonDefaults.Options);
        artifact!.Status.Should().Be(ArtifactStatus.FAILED);

        var download = await client.PostAsync(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts/{upload.ArtifactId}/download",
            null);
        download.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deleted_Artifact_Is_Removed_From_List_And_Cannot_Be_Downloaded()
    {
        var (client, projectId, buildId) = await RegisterWithBuildAsync();
        var created = await InitiateAsync(client, projectId, buildId);
        var upload = await created.Content.ReadFromJsonAsync<InitiateArtifactUploadResponse>(JsonDefaults.Options);

        var delete = await client.DeleteAsync(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts/{upload!.ArtifactId}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetFromJsonAsync<List<BuildArtifactResponse>>(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts",
            JsonDefaults.Options);
        list.Should().BeEmpty();

        var download = await client.PostAsync(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts/{upload.ArtifactId}/download",
            null);
        download.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpResponseMessage> InitiateAsync(HttpClient client, Guid projectId, Guid buildId) =>
        await client.PostAsJsonAsync(
            $"/api/projects/{projectId}/builds/{buildId}/artifacts/uploads",
            new InitiateArtifactUploadRequest(
                "client.zip",
                "Windows Client",
                ArtifactType.GAME_CLIENT,
                "application/zip",
                1024,
                null),
            JsonDefaults.Options);

    private async Task<(HttpClient Client, Guid ProjectId, Guid BuildId)> RegisterWithBuildAsync()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Victor", "SuperSecret123"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonDefaults.Options);
        client.DefaultRequestHeaders.Authorization = new("Bearer", auth!.AccessToken);

        var orgs = await client.GetFromJsonAsync<List<OrganizationResponse>>("/api/organizations", JsonDefaults.Options);
        var projectResponse = await client.PostAsJsonAsync(
            $"/api/organizations/{orgs![0].Id}/projects",
            new CreateProjectRequest("FRONTLINE", null));
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonDefaults.Options);

        var buildResponse = await client.PostAsJsonAsync(
            $"/api/projects/{project!.Id}/builds",
            new CreateBuildRequest(
                "QA Candidate",
                "0.4.2",
                Guid.NewGuid().ToString("N"),
                BuildPlatform.WINDOWS,
                BuildConfiguration.DEVELOPMENT,
                "main",
                "a941de3",
                "UE 5.8.2",
                "Artifact tests."),
            JsonDefaults.Options);
        var build = await buildResponse.Content.ReadFromJsonAsync<BuildResponse>(JsonDefaults.Options);

        return (client, project.Id, build!.Id);
    }
}
