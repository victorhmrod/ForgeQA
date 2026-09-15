using ForgeQA.Api.Extensions;
using ForgeQA.Application.ProjectApiKeys;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeQA.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:guid}/api-keys")]
public class ProjectApiKeysController : ControllerBase
{
    private readonly ProjectApiKeyService _apiKeyService;

    public ProjectApiKeysController(ProjectApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);

    [HttpPost]
    public async Task<ActionResult<ProjectApiKeyCreatedResponse>> Create(Guid projectId, CreateProjectApiKeyRequest request, CancellationToken cancellationToken)
    {
        var result = await _apiKeyService.CreateAsync(projectId, CurrentUserId, request, cancellationToken);
        if (!result.IsSuccess)
            return result.ToActionResult(this);

        return CreatedAtAction(nameof(GetForProject), new { projectId }, result.Value);
    }

    [HttpGet]
    public async Task<ActionResult<List<ProjectApiKeyResponse>>> GetForProject(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _apiKeyService.ListAsync(projectId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{keyId:guid}/revoke")]
    public async Task<ActionResult<ProjectApiKeyResponse>> Revoke(Guid projectId, Guid keyId, CancellationToken cancellationToken)
    {
        var result = await _apiKeyService.RevokeAsync(projectId, CurrentUserId, keyId, cancellationToken);
        return result.ToActionResult(this);
    }
}
