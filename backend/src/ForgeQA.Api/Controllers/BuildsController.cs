using ForgeQA.Api.Extensions;
using ForgeQA.Application.Builds;
using ForgeQA.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeQA.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:guid}/builds")]
public class BuildsController : ControllerBase
{
    private readonly BuildService _buildService;

    public BuildsController(BuildService buildService)
    {
        _buildService = buildService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);

    [HttpPost]
    public async Task<ActionResult<BuildResponse>> Create(Guid projectId, CreateBuildRequest request, CancellationToken cancellationToken)
    {
        var result = await _buildService.CreateAsync(projectId, CurrentUserId, request, cancellationToken);
        if (!result.IsSuccess)
            return result.ToActionResult(this);

        return CreatedAtAction(nameof(GetById), new { projectId, buildId = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<BuildResponse>>> GetForProject(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Domain.Enums.BuildPlatform? platform = null,
        [FromQuery] Domain.Enums.BuildConfiguration? configuration = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListBuildsRequest(page, pageSize, platform, configuration, status, search);
        var result = await _buildService.GetForProjectAsync(projectId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{buildId:guid}")]
    public async Task<ActionResult<BuildResponse>> GetById(Guid projectId, Guid buildId, CancellationToken cancellationToken)
    {
        var result = await _buildService.GetByIdAsync(projectId, buildId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPatch("{buildId:guid}")]
    public async Task<ActionResult<BuildResponse>> Update(Guid projectId, Guid buildId, UpdateBuildRequest request, CancellationToken cancellationToken)
    {
        var result = await _buildService.UpdateAsync(projectId, buildId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{buildId:guid}/archive")]
    public async Task<ActionResult<BuildResponse>> Archive(Guid projectId, Guid buildId, CancellationToken cancellationToken)
    {
        var result = await _buildService.ArchiveAsync(projectId, buildId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{buildId:guid}/restore")]
    public async Task<ActionResult<BuildResponse>> Restore(Guid projectId, Guid buildId, CancellationToken cancellationToken)
    {
        var result = await _buildService.RestoreAsync(projectId, buildId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }
}
