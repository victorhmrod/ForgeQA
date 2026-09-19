using ForgeQA.Api.Auth;
using ForgeQA.Api.Extensions;
using ForgeQA.Application.Common;
using ForgeQA.Application.Performance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ForgeQA.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/performance")]
public class PerformanceController : ControllerBase
{
    private readonly PerformanceService _performanceService;
    private readonly PerformanceOptions _options;

    public PerformanceController(PerformanceService performanceService, IOptions<PerformanceOptions> options)
    {
        _performanceService = performanceService;
        _options = options.Value;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);

    /// <summary>Runtime-only: authenticates exclusively via a Project API key holding
    /// PERFORMANCE_WRITE. Targets an existing telemetry session — never creates one.</summary>
    [HttpPost("sessions/{runtimeSessionId:guid}/samples")]
    [Authorize(AuthenticationSchemes = ProjectApiKeyDefaults.AuthenticationScheme)]
    [EnableRateLimiting(RateLimiting.PerformanceIngestionPolicy)]
    public async Task<ActionResult<IngestPerformanceSamplesResponse>> IngestSamples(
        Guid projectId, Guid runtimeSessionId, IngestPerformanceSamplesRequest request, CancellationToken cancellationToken)
    {
        if (Request.ContentLength is { } contentLength && contentLength > _options.MaxBatchBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, $"Batch exceeds the maximum size of {_options.MaxBatchBytes} bytes.");

        var result = await _performanceService.IngestAsync(projectId, runtimeSessionId, User.ToPerformanceIngestionCaller(), request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<PagedResult<PerformanceSessionListItemResponse>>> GetForProject(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? buildId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? platform = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListPerformanceSessionsRequest(page, pageSize, buildId, from, to, platform);
        var result = await _performanceService.GetForProjectAsync(projectId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<ActionResult<PerformanceSessionSummaryResponse>> GetSessionSummary(Guid projectId, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _performanceService.GetSessionSummaryAsync(projectId, sessionId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("sessions/{sessionId:guid}/samples")]
    [Authorize]
    public async Task<ActionResult<PagedResult<PerformanceSampleResponse>>> GetSamples(
        Guid projectId, Guid sessionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var result = await _performanceService.GetSamplesAsync(projectId, sessionId, CurrentUserId, page, pageSize, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("sessions/{sessionId:guid}/series")]
    [Authorize]
    public async Task<ActionResult<PerformanceSeriesResponse>> GetSeries(
        Guid projectId, Guid sessionId, [FromQuery] int maxPoints = 500, CancellationToken cancellationToken = default)
    {
        var result = await _performanceService.GetSeriesAsync(projectId, sessionId, CurrentUserId, maxPoints, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("sessions/{sessionId:guid}/maps")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ForgeQA.Application.Abstractions.MapBreakdownItem>>> GetMapBreakdown(Guid projectId, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _performanceService.GetMapBreakdownAsync(projectId, sessionId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("builds")]
    [Authorize]
    public async Task<ActionResult<List<BuildPerformanceListItemResponse>>> GetBuilds(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _performanceService.GetBuildsAsync(projectId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("compare")]
    [Authorize]
    public async Task<ActionResult<PerformanceCompareResponse>> Compare(
        Guid projectId, [FromQuery] Guid buildA, [FromQuery] Guid buildB, CancellationToken cancellationToken)
    {
        var result = await _performanceService.CompareAsync(projectId, CurrentUserId, buildA, buildB, cancellationToken);
        return result.ToActionResult(this);
    }
}
