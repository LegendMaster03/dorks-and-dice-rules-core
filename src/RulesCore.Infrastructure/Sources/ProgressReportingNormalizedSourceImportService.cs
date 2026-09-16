using RulesCore.Application.Sources;

namespace RulesCore.Infrastructure.Sources;

/// <summary>
/// Attaches a job-scoped advisory progress sink to normalized imports without coupling
/// CurrentUserSourceService to import-job persistence.
/// </summary>
public sealed class ProgressReportingNormalizedSourceImportService(
    INormalizedSourceImportService inner,
    Func<CurrentUserSourceImportProgress, CancellationToken, Task> reporter)
    : INormalizedSourceImportService
{
    public Task<NormalizedSourceImportResult> ImportAsync(
        ImportNormalizedSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return inner.ImportAsync(
            request with { ProgressReporter = reporter },
            cancellationToken);
    }
}
