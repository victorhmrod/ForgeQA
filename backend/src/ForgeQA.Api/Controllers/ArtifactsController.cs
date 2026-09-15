using ForgeQA.Api.Extensions;
using ForgeQA.Application.Artifacts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeQA.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:guid}/builds/{buildId:guid}/artifacts")]
public class ArtifactsController : ControllerBase
{
    private readonly ArtifactService _artifactService;

    public ArtifactsController(ArtifactService artifactService)
    {
        _artifactService = artifactService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);

    [HttpPost("uploads")]
    public async Task<ActionResult<InitiateArtifactUploadResponse>> InitiateUpload(
        Guid projectId,
        Guid buildId,
        InitiateArtifactUploadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _artifactService.InitiateUploadAsync(projectId, buildId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{artifactId:guid}/upload-parts")]
    public async Task<ActionResult<RequestUploadPartsResponse>> GetUploadPartUrls(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        RequestUploadPartsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _artifactService.GetUploadPartUrlsAsync(projectId, buildId, artifactId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{artifactId:guid}/complete")]
    public async Task<ActionResult<BuildArtifactResponse>> CompleteUpload(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        CompleteArtifactUploadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _artifactService.CompleteUploadAsync(projectId, buildId, artifactId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{artifactId:guid}/abort")]
    public async Task<ActionResult<BuildArtifactResponse>> AbortUpload(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        AbortArtifactUploadRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _artifactService.AbortUploadAsync(projectId, buildId, artifactId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BuildArtifactResponse>>> List(
        Guid projectId,
        Guid buildId,
        CancellationToken cancellationToken)
    {
        var result = await _artifactService.ListAsync(projectId, buildId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{artifactId:guid}")]
    public async Task<ActionResult<BuildArtifactResponse>> Get(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var result = await _artifactService.GetAsync(projectId, buildId, artifactId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{artifactId:guid}/download")]
    public async Task<ActionResult<ArtifactDownloadResponse>> Download(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var result = await _artifactService.CreateDownloadAsync(projectId, buildId, artifactId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{artifactId:guid}")]
    public async Task<ActionResult<BuildArtifactResponse>> Delete(
        Guid projectId,
        Guid buildId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var result = await _artifactService.DeleteAsync(projectId, buildId, artifactId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }
}
