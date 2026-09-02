using ForgeQA.Api.Extensions;
using ForgeQA.Application.Organizations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeQA.Api.Controllers;

[Authorize]
[Route("api/organizations")]
public class OrganizationsController : ApiControllerBase
{
    private readonly OrganizationService _organizationService;

    public OrganizationsController(OrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<OrganizationResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var organizations = await _organizationService.GetForUserAsync(CurrentUserId, cancellationToken);
        return Ok(organizations);
    }

    [HttpGet("{organizationId:guid}")]
    public async Task<ActionResult<OrganizationResponse>> GetById(Guid organizationId, CancellationToken cancellationToken)
    {
        var result = await _organizationService.GetByIdAsync(organizationId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }
}
