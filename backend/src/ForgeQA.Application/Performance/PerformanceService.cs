using ForgeQA.Application.Abstractions;
using ForgeQA.Application.Common;
using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ForgeQA.Application.Performance;

public class PerformanceService
{
    private static readonly string[] CompareMetrics =
    {
        "AverageFps", "MinimumFps", "AverageFrameTimeMs", "P50FrameTimeMs", "P95FrameTimeMs", "P99FrameTimeMs",
        "MaximumFrameTimeMs", "AverageMemoryUsedBytes", "AverageGameThreadTimeMs", "AverageRenderThreadTimeMs", "AverageGpuTimeMs"
    };

    private readonly IPerformanceRepository _performanceRepository;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly IBuildRepository _buildRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PerformanceOptions _options;

    public PerformanceService(
        IPerformanceRepository performanceRepository,
        ITelemetryRepository telemetryRepository,
        IBuildRepository buildRepository,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        IUnitOfWork unitOfWork,
        IOptions<PerformanceOptions> options)
    {
        _performanceRepository = performanceRepository;
        _telemetryRepository = telemetryRepository;
        _buildRepository = buildRepository;
        _projectRepository = projectRepository;
        _organizationRepository = organizationRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    /// <summary>Targets an existing TelemetrySession (Project + RuntimeSessionId) — performance
    /// ingestion never creates one. Arriving before the runtime calls telemetry's session-start is
    /// treated as a client lifecycle bug, not auto-corrected.</summary>
    public async Task<Result<IngestPerformanceSamplesResponse>> IngestAsync(
        Guid projectId, Guid runtimeSessionId, PerformanceIngestionCaller caller, IngestPerformanceSamplesRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeIngestionAsync(projectId, caller, cancellationToken);
        if (!access.IsSuccess)
            return Result<IngestPerformanceSamplesResponse>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByRuntimeSessionIdAsync(projectId, runtimeSessionId, cancellationToken);
        if (session is null)
            return Result<IngestPerformanceSamplesResponse>.Failure(ErrorType.NotFound, "Telemetry session not found. Start a telemetry session before submitting performance samples.");

        if (!session.IsActive)
            return Result<IngestPerformanceSamplesResponse>.Failure(ErrorType.Conflict, "This telemetry session has ended; new performance samples are rejected.");

        var samples = request.Samples;
        if (samples is null || samples.Count == 0)
            return Result<IngestPerformanceSamplesResponse>.Failure(ErrorType.Validation, "At least one sample is required.");
        if (samples.Count > _options.MaxSamplesPerBatch)
            return Result<IngestPerformanceSamplesResponse>.Failure(ErrorType.Validation, $"A batch may contain at most {_options.MaxSamplesPerBatch} samples.");

        var sequenceNumbers = samples.Select(s => s.SequenceNumber).ToList();
        if (sequenceNumbers.Distinct().Count() != sequenceNumbers.Count)
            return Result<IngestPerformanceSamplesResponse>.Failure(ErrorType.Validation, "Batch contains duplicate sequence numbers.");

        var built = new List<PerformanceSample>(samples.Count);
        foreach (var sampleRequest in samples)
        {
            try
            {
                built.Add(new PerformanceSample(
                    session.Id, sampleRequest.SequenceNumber, sampleRequest.ClientTimestamp,
                    sampleRequest.Fps, sampleRequest.FrameTimeMs, sampleRequest.MapName,
                    sampleRequest.GameThreadTimeMs, sampleRequest.RenderThreadTimeMs, sampleRequest.GpuTimeMs,
                    sampleRequest.MemoryUsedBytes, sampleRequest.MemoryAvailableBytes,
                    sampleRequest.CpuUtilizationPercent, sampleRequest.GpuUtilizationPercent,
                    sampleRequest.DrawCalls, sampleRequest.PlayerCount, sampleRequest.PingMs));
            }
            catch (ArgumentException ex)
            {
                return Result<IngestPerformanceSamplesResponse>.Failure(ErrorType.Validation, ex.Message);
            }
        }

        var existingSequences = await _performanceRepository.GetExistingSequenceNumbersAsync(session.Id, sequenceNumbers, cancellationToken);
        var toInsert = built.Where(s => !existingSequences.Contains(s.SequenceNumber)).ToList();

        if (toInsert.Count > 0)
        {
            _performanceRepository.AddSamples(toInsert);
            session.RecordEventsReceived();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<IngestPerformanceSamplesResponse>.Success(new IngestPerformanceSamplesResponse(toInsert.Count, existingSequences.Count));
    }

    public async Task<Result<PerformanceSessionSummaryResponse>> GetSessionSummaryAsync(Guid projectId, Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PerformanceSessionSummaryResponse>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByIdForProjectAsync(projectId, sessionId, cancellationToken);
        if (session is null)
            return Result<PerformanceSessionSummaryResponse>.Failure(ErrorType.NotFound, "Telemetry session not found.");

        var build = await _buildRepository.GetByIdForProjectAsync(projectId, session.BuildId, cancellationToken);
        var summary = await _performanceRepository.GetSummaryAsync(session.Id, cancellationToken);

        return Result<PerformanceSessionSummaryResponse>.Success(
            new PerformanceSessionSummaryResponse(session.Id, session.RuntimeSessionId, ToBuildSummary(build!), summary));
    }

    public async Task<Result<PagedResult<PerformanceSampleResponse>>> GetSamplesAsync(
        Guid projectId, Guid sessionId, Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PagedResult<PerformanceSampleResponse>>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByIdForProjectAsync(projectId, sessionId, cancellationToken);
        if (session is null)
            return Result<PagedResult<PerformanceSampleResponse>>.Failure(ErrorType.NotFound, "Telemetry session not found.");

        const int defaultPageSize = 100;
        const int maxPageSize = 500;
        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize <= 0 ? defaultPageSize : Math.Min(pageSize, maxPageSize);

        var (items, totalCount) = await _performanceRepository.QuerySamplesAsync(session.Id, effectivePage, effectivePageSize, cancellationToken);
        return Result<PagedResult<PerformanceSampleResponse>>.Success(
            new PagedResult<PerformanceSampleResponse>(items.Select(ToSampleResponse).ToList(), effectivePage, effectivePageSize, totalCount));
    }

    public async Task<Result<PerformanceSeriesResponse>> GetSeriesAsync(Guid projectId, Guid sessionId, Guid userId, int maxPoints, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PerformanceSeriesResponse>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByIdForProjectAsync(projectId, sessionId, cancellationToken);
        if (session is null)
            return Result<PerformanceSeriesResponse>.Failure(ErrorType.NotFound, "Telemetry session not found.");

        var effectiveMaxPoints = maxPoints <= 0 ? 500 : Math.Min(maxPoints, 2000);
        var points = await _performanceRepository.GetSeriesAsync(session.Id, effectiveMaxPoints, cancellationToken);
        return Result<PerformanceSeriesResponse>.Success(new PerformanceSeriesResponse(points));
    }

    public async Task<Result<IReadOnlyList<MapBreakdownItem>>> GetMapBreakdownAsync(Guid projectId, Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<IReadOnlyList<MapBreakdownItem>>.Failure(access.ErrorType, access.Error!);

        var session = await _telemetryRepository.GetByIdForProjectAsync(projectId, sessionId, cancellationToken);
        if (session is null)
            return Result<IReadOnlyList<MapBreakdownItem>>.Failure(ErrorType.NotFound, "Telemetry session not found.");

        var breakdown = await _performanceRepository.GetMapBreakdownAsync(session.Id, cancellationToken);
        return Result<IReadOnlyList<MapBreakdownItem>>.Success(breakdown);
    }

    public async Task<Result<PagedResult<PerformanceSessionListItemResponse>>> GetForProjectAsync(
        Guid projectId, Guid userId, ListPerformanceSessionsRequest request, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PagedResult<PerformanceSessionListItemResponse>>.Failure(access.ErrorType, access.Error!);

        const int defaultPageSize = 20;
        const int maxPageSize = 100;
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? defaultPageSize : Math.Min(request.PageSize, maxPageSize);

        var query = new PerformanceSessionQuery(projectId, page, pageSize, request.BuildId, request.From, request.To, request.Platform);
        var (sessions, totalCount) = await _performanceRepository.QuerySessionsWithSamplesAsync(query, cancellationToken);

        var buildIds = sessions.Select(s => s.BuildId).Distinct().ToList();
        var builds = buildIds.Count == 0 ? new List<Build>() : await _buildRepository.GetByIdsAsync(buildIds, cancellationToken);
        var buildsById = builds.ToDictionary(b => b.Id);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var summaries = await _performanceRepository.GetSummariesForSessionsAsync(sessionIds, cancellationToken);

        var items = sessions.Select(session =>
        {
            var build = buildsById.TryGetValue(session.BuildId, out var b) ? ToBuildSummary(b) : null;
            var summary = summaries.TryGetValue(session.Id, out var s) ? s : EmptySummary();
            return new PerformanceSessionListItemResponse(
                session.Id, session.RuntimeSessionId, build!, session.StartedAt, session.EndedAt, session.Environment.Platform, summary);
        }).ToList();

        return Result<PagedResult<PerformanceSessionListItemResponse>>.Success(new PagedResult<PerformanceSessionListItemResponse>(items, page, pageSize, totalCount));
    }

    public async Task<Result<List<BuildPerformanceListItemResponse>>> GetBuildsAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<List<BuildPerformanceListItemResponse>>.Failure(access.ErrorType, access.Error!);

        var buildSummaries = await _performanceRepository.GetBuildSummariesAsync(projectId, cancellationToken);
        var buildIds = buildSummaries.Select(b => b.BuildId).ToList();
        var builds = buildIds.Count == 0 ? new List<Build>() : await _buildRepository.GetByIdsAsync(buildIds, cancellationToken);
        var buildsById = builds.ToDictionary(b => b.Id);

        var items = buildSummaries
            .Where(bs => buildsById.ContainsKey(bs.BuildId))
            .Select(bs =>
            {
                var build = buildsById[bs.BuildId];
                return new BuildPerformanceListItemResponse(
                    build.Id, build.Version, build.BuildNumber, build.Platform, build.Configuration,
                    build.IsArchived, bs.SessionCount, bs.Summary);
            })
            .ToList();

        return Result<List<BuildPerformanceListItemResponse>>.Success(items);
    }

    public async Task<Result<PerformanceCompareResponse>> CompareAsync(Guid projectId, Guid userId, Guid buildAId, Guid buildBId, CancellationToken cancellationToken)
    {
        var access = await AuthorizeForUserAsync(projectId, userId, cancellationToken);
        if (!access.IsSuccess)
            return Result<PerformanceCompareResponse>.Failure(access.ErrorType, access.Error!);

        var buildA = await _buildRepository.GetByIdForProjectAsync(projectId, buildAId, cancellationToken);
        if (buildA is null)
            return Result<PerformanceCompareResponse>.Failure(ErrorType.NotFound, "Build A not found for this project.");

        var buildB = await _buildRepository.GetByIdForProjectAsync(projectId, buildBId, cancellationToken);
        if (buildB is null)
            return Result<PerformanceCompareResponse>.Failure(ErrorType.NotFound, "Build B not found for this project.");

        var summaryA = await _performanceRepository.GetBuildSummaryAsync(projectId, buildAId, cancellationToken) ?? EmptySummary();
        var summaryB = await _performanceRepository.GetBuildSummaryAsync(projectId, buildBId, cancellationToken) ?? EmptySummary();

        var buildSummaries = await _performanceRepository.GetBuildSummariesAsync(projectId, cancellationToken);
        var sessionCountA = buildSummaries.FirstOrDefault(b => b.BuildId == buildAId)?.SessionCount ?? 0;
        var sessionCountB = buildSummaries.FirstOrDefault(b => b.BuildId == buildBId)?.SessionCount ?? 0;

        var responseA = new BuildPerformanceListItemResponse(buildA.Id, buildA.Version, buildA.BuildNumber, buildA.Platform, buildA.Configuration, buildA.IsArchived, sessionCountA, summaryA);
        var responseB = new BuildPerformanceListItemResponse(buildB.Id, buildB.Version, buildB.BuildNumber, buildB.Platform, buildB.Configuration, buildB.IsArchived, sessionCountB, summaryB);

        var metrics = CompareMetrics.Select(metricName =>
        {
            var valueA = GetMetricValue(summaryA, metricName);
            var valueB = GetMetricValue(summaryB, metricName);

            double? delta = valueA is { } a && valueB is { } b ? b - a : null;
            double? deltaPercent = delta is { } d && valueA is { } baseline
                ? (baseline == 0 ? null : Math.Round(d / baseline * 100, 2))
                : null;

            return new PerformanceCompareMetric(metricName, valueA, valueB, delta, deltaPercent);
        }).ToList();

        return Result<PerformanceCompareResponse>.Success(new PerformanceCompareResponse(responseA, responseB, metrics));
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

    private async Task<Result<Project>> AuthorizeIngestionAsync(Guid projectId, PerformanceIngestionCaller caller, CancellationToken cancellationToken)
    {
        if (caller.ProjectId != projectId)
            return Result<Project>.Failure(ErrorType.Forbidden, "This Project API key is not authorized for this project.");
        if (!caller.HasScope(ProjectApiKeyScope.PERFORMANCE_WRITE))
            return Result<Project>.Failure(ErrorType.Forbidden, "This Project API key does not have the PERFORMANCE_WRITE scope.");

        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        return project is null
            ? Result<Project>.Failure(ErrorType.NotFound, "Project not found.")
            : Result<Project>.Success(project);
    }

    private static double? GetMetricValue(PerformanceSummary summary, string metricName) => metricName switch
    {
        "AverageFps" => summary.AverageFps,
        "MinimumFps" => summary.MinimumFps,
        "AverageFrameTimeMs" => summary.AverageFrameTimeMs,
        "P50FrameTimeMs" => summary.P50FrameTimeMs,
        "P95FrameTimeMs" => summary.P95FrameTimeMs,
        "P99FrameTimeMs" => summary.P99FrameTimeMs,
        "MaximumFrameTimeMs" => summary.MaximumFrameTimeMs,
        "AverageMemoryUsedBytes" => summary.AverageMemoryUsedBytes,
        "AverageGameThreadTimeMs" => summary.AverageGameThreadTimeMs,
        "AverageRenderThreadTimeMs" => summary.AverageRenderThreadTimeMs,
        "AverageGpuTimeMs" => summary.AverageGpuTimeMs,
        _ => null
    };

    private static PerformanceSummary EmptySummary() => new(0, null, null, null, null, null, null, null, null, null, null, null, null);

    private static PerformanceBuildSummaryResponse ToBuildSummary(Build build) => new(build.Id, build.Version, build.BuildNumber, build.Platform, build.Configuration);

    private static PerformanceSampleResponse ToSampleResponse(PerformanceSample sample) => new(
        sample.Id, sample.SequenceNumber, sample.ClientTimestamp, sample.ReceivedAt, sample.MapName,
        sample.Fps, sample.FrameTimeMs, sample.GameThreadTimeMs, sample.RenderThreadTimeMs, sample.GpuTimeMs,
        sample.MemoryUsedBytes, sample.MemoryAvailableBytes, sample.CpuUtilizationPercent, sample.GpuUtilizationPercent,
        sample.DrawCalls, sample.PlayerCount, sample.PingMs);
}
