using ForgeQA.Api.Extensions;
using ForgeQA.Application.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeQA.Api.Controllers;

[Authorize]
[ApiController]
public class ProjectsController : ControllerBase
{
    private readonly ProjectService _projectService;

    public ProjectsController(ProjectService projectService)
    {
        _projectService = projectService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);

    [HttpPost("api/organizations/{organizationId:guid}/projects")]
    public async Task<ActionResult<ProjectResponse>> Create(Guid organizationId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _projectService.CreateAsync(organizationId, CurrentUserId, request, cancellationToken);
        if (!result.IsSuccess)
            return result.ToActionResult(this);

        return CreatedAtAction(nameof(GetById), new { projectId = result.Value!.Id }, result.Value);
    }

    [HttpGet("api/organizations/{organizationId:guid}/projects")]
    public async Task<ActionResult<List<ProjectResponse>>> GetForOrganization(Guid organizationId, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetForOrganizationAsync(organizationId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("api/projects/{projectId:guid}")]
    public async Task<ActionResult<ProjectResponse>> GetById(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetByIdAsync(projectId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }
}
