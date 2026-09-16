using ForgeQA.Api.Auth;
using ForgeQA.Api.Extensions;
using ForgeQA.Application.Common;
using ForgeQA.Application.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ForgeQA.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly TelemetryService _telemetryService;
    private readonly TelemetryOptions _options;

    public TelemetryController(TelemetryService telemetryService, IOptions<TelemetryOptions> options)
    {
        _telemetryService = telemetryService;
        _options = options.Value;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);

    /// <summary>Runtime-only: authenticates exclusively via a Project API key holding
    /// TELEMETRY_WRITE. Idempotent — replaying the same runtimeSessionId+buildId returns the
    /// existing session rather than creating a duplicate.</summary>
    [HttpPost("sessions")]
    [Authorize(AuthenticationSchemes = ProjectApiKeyDefaults.AuthenticationScheme)]
    [EnableRateLimiting(RateLimiting.TelemetryIngestionPolicy)]
    public async Task<ActionResult<TelemetrySessionResponse>> StartSession(Guid projectId, StartTelemetrySessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _telemetryService.StartSessionAsync(projectId, User.ToTelemetryIngestionCaller(), request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("sessions/{runtimeSessionId:guid}/events")]
    [Authorize(AuthenticationSchemes = ProjectApiKeyDefaults.AuthenticationScheme)]
    [EnableRateLimiting(RateLimiting.TelemetryIngestionPolicy)]
    public async Task<ActionResult<IngestTelemetryEventsResponse>> IngestEvents(
        Guid projectId, Guid runtimeSessionId, IngestTelemetryEventsRequest request, CancellationToken cancellationToken)
    {
        // Rejected before any DB work: cheap, request-level protection against oversized payloads.
        if (Request.ContentLength is { } contentLength && contentLength > _options.MaxBatchBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, $"Batch exceeds the maximum size of {_options.MaxBatchBytes} bytes.");

        var result = await _telemetryService.IngestEventsAsync(projectId, runtimeSessionId, User.ToTelemetryIngestionCaller(), request, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Idempotent — ending an already-ended session repeats the same success response.</summary>
    [HttpPost("sessions/{runtimeSessionId:guid}/end")]
    [Authorize(AuthenticationSchemes = ProjectApiKeyDefaults.AuthenticationScheme)]
    [EnableRateLimiting(RateLimiting.TelemetryIngestionPolicy)]
    public async Task<ActionResult<TelemetrySessionResponse>> EndSession(Guid projectId, Guid runtimeSessionId, CancellationToken cancellationToken)
    {
        var result = await _telemetryService.EndSessionAsync(projectId, runtimeSessionId, User.ToTelemetryIngestionCaller(), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<PagedResult<TelemetrySessionListItemResponse>>> GetForProject(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? buildId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? runtimeSessionId = null,
        [FromQuery] bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListTelemetrySessionsRequest(page, pageSize, buildId, from, to, runtimeSessionId, activeOnly);
        var result = await _telemetryService.GetForProjectAsync(projectId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<ActionResult<TelemetrySessionResponse>> GetById(Guid projectId, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _telemetryService.GetByIdAsync(projectId, sessionId, CurrentUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("sessions/{sessionId:guid}/events")]
    [Authorize]
    public async Task<ActionResult<PagedResult<TelemetryEventResponse>>> GetEvents(
        Guid projectId,
        Guid sessionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? eventName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListTelemetryEventsRequest(page, pageSize, eventName);
        var result = await _telemetryService.GetEventsAsync(projectId, sessionId, CurrentUserId, request, cancellationToken);
        return result.ToActionResult(this);
    }
}
