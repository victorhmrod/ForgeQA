using System.Text;
using System.Text.Json;
using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ForgeQA.Application.Telemetry;

public class TelemetryService
{
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly IBuildRepository _buildRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TelemetryOptions _options;

    public TelemetryService(
        ITelemetryRepository telemetryRepository,
        IBuildRepository buildRepository,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        IUnitOfWork unitOfWork,
        IOptions<TelemetryOptions> options)
    {
        _telemetryRepository = telemetryRepository;
        _buildRepository = buildRepository;
        _projectRepository = projectRepository;
        _organizationRepository = organizationRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    /// <summary>Idempotent: replaying the same ProjectId+RuntimeSessionId with the same BuildId
    /// returns the existing session rather than creating a duplicate. A retry that changes the
    /// BuildId is rejected — session identity is never silently mutated.</summary>
    public async Task<Result<TelemetrySessionResponse>> StartSessionAsync(
        Guid projectId, TelemetryIngestionCaller caller, StartTelemetrySessionRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeIngestionAsync(projectId, caller, cancellationToken);
        if (!access.IsSuccess)
            return Result<TelemetrySessionResponse>.Failure(access.ErrorType, access.Error!);

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, request.BuildId, cancellationToken);
        if (build is null)
            return Result<TelemetrySessionResponse>.Failure(ErrorType.Validation, "Build not found for this project.");

        var existing = await _telemetryRepository.GetByRuntimeSessionIdAsync(projectId, request.RuntimeSessionId, cancellationToken);
        if (existing is not null)
        {
            if (existing.BuildId != request.BuildId)
                return Result<TelemetrySessionResponse>.Failure(
                    ErrorType.Conflict, "This runtime session is already associated with a different Build.");

            return Result<TelemetrySessionResponse>.Success(await ToResponseAsync(existing, build, cancellationToken));
        }

        TelemetrySession session;
        try
        {
            session = new TelemetrySession(projectId, request.BuildId, request.RuntimeSessionId, ToDomainEnvironment(request.Environment));
        }
        catch (ArgumentException ex)
        {
            return Result<TelemetrySessionResponse>.Failure(ErrorType.Validation, ex.Message);
        }

        _telemetryRepository.Add(session);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<TelemetrySessionResponse>.Success(await ToResponseAsync(session, build, cancellationToken));
    }

    /// <summary>Validates the entire batch before inserting anything — an invalid event fails the
    /// whole batch, which is easier for clients to reason about on retry than partial acceptance.
    /// Already-ingested sequence numbers are treated as duplicates, not errors, so a retried batch
    /// is always safe to resend.</summary>
    public async Task<Result<IngestTelemetryEventsResponse>> IngestEventsAsync(
        Guid projectId, Guid runtimeSessionId, TelemetryIngestionCaller caller, IngestTelemetryEventsRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeIngestionAsync(projectId, caller, cancellationToken);
        if (!access.IsSuccess)
            return Result<IngestTelemetryEventsResponse>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByRuntimeSessionIdAsync(projectId, runtimeSessionId, cancellationToken);
        if (session is null)
            return Result<IngestTelemetryEventsResponse>.Failure(ErrorType.NotFound, "Telemetry session not found.");

        if (!session.IsActive)
            return Result<IngestTelemetryEventsResponse>.Failure(ErrorType.Conflict, "This telemetry session has ended; new events are rejected.");

        var events = request.Events;
        if (events is null || events.Count == 0)
            return Result<IngestTelemetryEventsResponse>.Failure(ErrorType.Validation, "At least one event is required.");
        if (events.Count > _options.MaxEventsPerBatch)
            return Result<IngestTelemetryEventsResponse>.Failure(ErrorType.Validation, $"A batch may contain at most {_options.MaxEventsPerBatch} events.");

        var sequenceNumbers = events.Select(e => e.SequenceNumber).ToList();
        if (sequenceNumbers.Distinct().Count() != sequenceNumbers.Count)
            return Result<IngestTelemetryEventsResponse>.Failure(ErrorType.Validation, "Batch contains duplicate sequence numbers.");

        var built = new List<TelemetryEvent>(events.Count);
        foreach (var eventRequest in events)
        {
            string propertiesJson;
            if (eventRequest.Properties is { } properties)
            {
                if (properties.ValueKind != JsonValueKind.Object)
                    return Result<IngestTelemetryEventsResponse>.Failure(ErrorType.Validation, "Event properties must be a JSON object.");

                propertiesJson = properties.GetRawText();
                if (Encoding.UTF8.GetByteCount(propertiesJson) > _options.MaxEventPropertiesBytes)
                    return Result<IngestTelemetryEventsResponse>.Failure(
                        ErrorType.Validation, $"Event properties exceed the maximum size of {_options.MaxEventPropertiesBytes} bytes.");
            }
            else
            {
                propertiesJson = "{}";
            }

            try
            {
                built.Add(new TelemetryEvent(
                    session.Id, eventRequest.SequenceNumber, eventRequest.EventName, eventRequest.ClientTimestamp,
                    eventRequest.Category, propertiesJson, eventRequest.MapName));
            }
            catch (ArgumentException ex)
            {
                return Result<IngestTelemetryEventsResponse>.Failure(ErrorType.Validation, ex.Message);
            }
        }

        var existingSequences = await _telemetryRepository.GetExistingSequenceNumbersAsync(session.Id, sequenceNumbers, cancellationToken);
        var toInsert = built.Where(e => !existingSequences.Contains(e.SequenceNumber)).ToList();

        if (toInsert.Count > 0)
        {
            _telemetryRepository.AddEvents(toInsert);
            session.RecordEventsReceived();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<IngestTelemetryEventsResponse>.Success(new IngestTelemetryEventsResponse(toInsert.Count, existingSequences.Count));
    }

    public async Task<Result<TelemetrySessionResponse>> EndSessionAsync(
        Guid projectId, Guid runtimeSessionId, TelemetryIngestionCaller caller, CancellationToken cancellationToken)
    {
        var access = await AuthorizeIngestionAsync(projectId, caller, cancellationToken);
        if (!access.IsSuccess)
            return Result<TelemetrySessionResponse>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByRuntimeSessionIdAsync(projectId, runtimeSessionId, cancellationToken);
        if (session is null)
            return Result<TelemetrySessionResponse>.Failure(ErrorType.NotFound, "Telemetry session not found.");

        session.End();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, session.BuildId, cancellationToken);
        return Result<TelemetrySessionResponse>.Success(await ToResponseAsync(session, build!, cancellationToken));
    }

    public async Task<Result<PagedResult<TelemetrySessionListItemResponse>>> GetForProjectAsync(
        Guid projectId, Guid userId, ListTelemetrySessionsRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PagedResult<TelemetrySessionListItemResponse>>.Failure(access.ErrorType, access.Error!);

        const int defaultPageSize = 20;
        const int maxPageSize = 100;
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? defaultPageSize : Math.Min(request.PageSize, maxPageSize);

        var query = new TelemetrySessionQuery(projectId, page, pageSize, request.BuildId, request.From, request.To, request.RuntimeSessionId, request.ActiveOnly);
        var (items, totalCount) = await _telemetryRepository.QueryAsync(query, cancellationToken);

        var buildIds = items.Select(i => i.Session.BuildId).Distinct().ToList();
        var builds = buildIds.Count == 0 ? new List<Build>() : await _buildRepository.GetByIdsAsync(buildIds, cancellationToken);
        var buildsById = builds.ToDictionary(b => b.Id);

        var listItems = items.Select(item =>
        {
            var build = buildsById.TryGetValue(item.Session.BuildId, out var b) ? ToBuildSummary(b) : null;
            return new TelemetrySessionListItemResponse(
                item.Session.Id, item.Session.RuntimeSessionId, build!, item.Session.StartedAt, item.Session.EndedAt,
                item.Session.LastEventAt, item.EventCount, item.Session.Environment.Platform, item.Session.Environment.MapName);
        }).ToList();

        return Result<PagedResult<TelemetrySessionListItemResponse>>.Success(new PagedResult<TelemetrySessionListItemResponse>(listItems, page, pageSize, totalCount));
    }

    public async Task<Result<TelemetrySessionResponse>> GetByIdAsync(Guid projectId, Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<TelemetrySessionResponse>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByIdForProjectAsync(projectId, sessionId, cancellationToken);
        if (session is null)
            return Result<TelemetrySessionResponse>.Failure(ErrorType.NotFound, "Telemetry session not found.");

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, session.BuildId, cancellationToken);
        return Result<TelemetrySessionResponse>.Success(await ToResponseAsync(session, build!, cancellationToken));
    }

    public async Task<Result<PagedResult<TelemetryEventResponse>>> GetEventsAsync(
        Guid projectId, Guid sessionId, Guid userId, ListTelemetryEventsRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PagedResult<TelemetryEventResponse>>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByIdForProjectAsync(projectId, sessionId, cancellationToken);
        if (session is null)
            return Result<PagedResult<TelemetryEventResponse>>.Failure(ErrorType.NotFound, "Telemetry session not found.");

        const int defaultPageSize = 100;
        const int maxPageSize = 500;
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? defaultPageSize : Math.Min(request.PageSize, maxPageSize);

        var (items, totalCount) = await _telemetryRepository.QueryEventsAsync(session.Id, page, pageSize, request.EventName, cancellationToken);
        var responses = items.Select(ToEventResponse).ToList();

        return Result<PagedResult<TelemetryEventResponse>>.Success(new PagedResult<TelemetryEventResponse>(responses, page, pageSize, totalCount));
    }

    private async Task<Result<Project>> AuthorizeForUserAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
            return Result<Project>.Failure(ErrorType.NotFound, "Project not found.");

        var membership = await _organizationRepository.GetMembershipAsync(project.OrganizationId, userId, cancellationToken);
        if (membership is null)
            return Result<Project>.Failure(ErrorType.Forbidden, "You are not a member of this project's organization.");

        return Result<Project>.Success(project);
    }

    /// <summary>A Project API key must be bound to exactly this Project AND hold TELEMETRY_WRITE —
    /// a key with only BUG_REPORT_WRITE (or one issued for a different Project) is rejected here,
    /// before any session/event work is attempted.</summary>
    private async Task<Result<Project>> AuthorizeIngestionAsync(Guid projectId, TelemetryIngestionCaller caller, CancellationToken cancellationToken)
    {
        if (caller.ProjectId != projectId)
            return Result<Project>.Failure(ErrorType.Forbidden, "This Project API key is not authorized for this project.");
        if (!caller.HasScope(ProjectApiKeyScope.TELEMETRY_WRITE))
            return Result<Project>.Failure(ErrorType.Forbidden, "This Project API key does not have the TELEMETRY_WRITE scope.");

        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        return project is null
            ? Result<Project>.Failure(ErrorType.NotFound, "Project not found.")
            : Result<Project>.Success(project);
    }

    private async Task<TelemetrySessionResponse> ToResponseAsync(TelemetrySession session, Build build, CancellationToken cancellationToken)
    {
        var eventCount = await _telemetryRepository.GetEventCountAsync(session.Id, cancellationToken);
        return new TelemetrySessionResponse(
            session.Id, session.RuntimeSessionId, ToBuildSummary(build), session.StartedAt, session.EndedAt,
            session.LastEventAt, eventCount, ToEnvironmentDto(session.Environment));
    }

    private static TelemetrySessionEnvironment? ToDomainEnvironment(TelemetrySessionEnvironmentDto? dto) => dto is null
        ? null
        : new TelemetrySessionEnvironment(dto.MapName, dto.GameMode, dto.Platform, dto.Configuration, dto.EngineVersion, dto.OsVersion, dto.Locale);

    private static TelemetrySessionEnvironmentDto ToEnvironmentDto(TelemetrySessionEnvironment environment) => new(
        environment.MapName, environment.GameMode, environment.Platform, environment.Configuration,
        environment.EngineVersion, environment.OsVersion, environment.Locale);

    private static TelemetryBuildSummaryResponse ToBuildSummary(Build build) => new(build.Id, build.Version, build.BuildNumber, build.Platform, build.Configuration);

    private static TelemetryEventResponse ToEventResponse(TelemetryEvent evt) => new(
        evt.Id, evt.SequenceNumber, evt.EventName, evt.ClientTimestamp, evt.ReceivedAt, evt.Category,
        JsonDocument.Parse(evt.PropertiesJson).RootElement, evt.MapName);
}
