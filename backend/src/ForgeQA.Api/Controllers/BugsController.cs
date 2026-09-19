using ForgeQA.Api.Auth;
using ForgeQA.Api.Extensions;
using ForgeQA.Application.Bugs;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ForgeQA.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/bugs")]
public class BugsController : ControllerBase
{
    private const string DualAuthSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ProjectApiKeyDefaults.AuthenticationScheme}";

    private readonly BugService _bugService;

    public BugsController(BugService bugService)
    {
        _bugService = bugService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);

    /// <summary>Accepts either an authenticated dashboard user (WEB/API source) or a Project API key
    /// (UNREAL_RUNTIME/API source) — see <see cref="BugReportAuthorExtensions.ToBugReportAuthor"/>.</summary>
    [HttpPost]
    [Authorize(AuthenticationSchemes = DualAuthSchemes)]
    [EnableRateLimiting(RateLimiting.BugIngestionPolicy)]
    public async Task<ActionResult<BugResponse>> Create(Guid projectId, CreateBugRequest request, CancellationToken cancellationToken)
    {
        var result = await _bugService.CreateAsync(projectId, User.ToBugReportAuthor(), request, cancellationToken);
        if (!result.IsSuccess)
            return result.ToActionResult(this);

        return CreatedAtAction(nameof(GetById), new { projectId, bugId = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PagedResult<BugListItemResponse>>> GetForProject(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] BugStatus? status = null,
        [FromQuery] BugSeverity? severity = null,
        [FromQuery] Guid? buildId = null,
        [FromQuery] BugSource? source = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? runtimeSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListBugsRequest(page, pageSize, status, severity, buildId, source, search, runtimeSessionId);
        var result = await _bugService.GetForProjectAsync(projectId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{bugId:guid}")]
    [Authorize]
    public async Task<ActionResult<BugResponse>> GetById(Guid projectId, Guid bugId, CancellationToken cancellationToken)
    {
        var result = await _bugService.GetByIdAsync(projectId, bugId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{bugId:guid}")]
    [Authorize]
    public async Task<ActionResult<BugResponse>> Update(Guid projectId, Guid bugId, UpdateBugRequest request, CancellationToken cancellationToken)
    {
        var result = await _bugService.UpdateAsync(projectId, bugId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{bugId:guid}/attachments")]
    [Authorize(AuthenticationSchemes = DualAuthSchemes)]
    [EnableRateLimiting(RateLimiting.BugIngestionPolicy)]
    public async Task<ActionResult<InitiateBugAttachmentResponse>> InitiateAttachment(
        Guid projectId, Guid bugId, InitiateBugAttachmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _bugService.InitiateAttachmentAsync(projectId, bugId, User.ToBugReportAuthor(), request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{bugId:guid}/attachments/{attachmentId:guid}/complete")]
    [Authorize(AuthenticationSchemes = DualAuthSchemes)]
    [EnableRateLimiting(RateLimiting.BugIngestionPolicy)]
    public async Task<ActionResult<BugAttachmentResponse>> CompleteAttachment(
        Guid projectId, Guid bugId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await _bugService.CompleteAttachmentAsync(projectId, bugId, attachmentId, User.ToBugReportAuthor(), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{bugId:guid}/attachments/{attachmentId:guid}/download")]
    [Authorize]
    public async Task<ActionResult<BugAttachmentDownloadResponse>> CreateAttachmentDownload(
        Guid projectId, Guid bugId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await _bugService.CreateAttachmentDownloadAsync(projectId, bugId, attachmentId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }
}
