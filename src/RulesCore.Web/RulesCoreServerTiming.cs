using System.Diagnostics;
using System.Globalization;

namespace RulesCore.Web;

public static class RulesCoreServerTiming
{
    public const string HeaderName = "Server-Timing";
    public const string RequestMetricName = "rules-core";
    public const string AuthenticationMetricName = "rules-core-auth";
    public const string DatabaseMetricName = "rules-core-db";
    public const string ReferenceQueryMetricName = "rules-core-reference-query";
    public const string ReferenceMaterializationMetricName = "rules-core-reference-materialize";
    public const string ReferenceTotalMetricName = "rules-core-reference-total";

    private const string NpgsqlActivitySourceName = "Npgsql";
    private const string NpgsqlConnectionActivityPrefix = "CONNECT ";
    private const string RequestStateActivityPropertyName = "RulesCore.ServerTiming.RequestState";

    private static readonly object RequestStateKey = new();
    private static readonly AsyncLocal<RequestTimingState?> CurrentRequestState = new();
    private static readonly Lazy<ActivityListener> NpgsqlActivityListener = new(
        CreateNpgsqlActivityListener,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static void EnsureRequestTiming(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(RequestStateKey, out var existing)
            && existing is RequestTimingState existingState)
        {
            CurrentRequestState.Value = existingState;
            return;
        }

        EnsureNpgsqlActivityListener();

        var state = new RequestTimingState(Stopwatch.GetTimestamp());
        httpContext.Items[RequestStateKey] = state;
        CurrentRequestState.Value = state;

        httpContext.Response.OnStarting(() =>
        {
            var databaseSnapshot = state.CompleteAndSnapshot();
            ClearCurrentRequestState(state);

            if (databaseSnapshot.OperationCount > 0)
            {
                AppendDuration(
                    httpContext,
                    DatabaseMetricName,
                    TimeSpan.FromTicks(databaseSnapshot.Ticks).TotalMilliseconds);
            }

            AppendDuration(
                httpContext,
                RequestMetricName,
                Stopwatch.GetElapsedTime(state.StartedAt).TotalMilliseconds);
            return Task.CompletedTask;
        });

        httpContext.Response.OnCompleted(() =>
        {
            CompleteRequestTiming(httpContext);
            return Task.CompletedTask;
        });
    }

    public static void CompleteRequestTiming(HttpContext httpContext)
    {
        if (!httpContext.Items.TryGetValue(RequestStateKey, out var existing)
            || existing is not RequestTimingState state)
        {
            return;
        }

        state.Complete();
        ClearCurrentRequestState(state);
    }

    public static void AppendDuration(
        HttpContext httpContext,
        string metricName,
        double durationMilliseconds)
    {
        if (!double.IsFinite(durationMilliseconds) || durationMilliseconds < 0)
        {
            return;
        }

        var metric = $"{metricName};dur={durationMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)}";
        httpContext.Response.Headers.Append(HeaderName, metric);
    }

    private static void EnsureNpgsqlActivityListener() =>
        _ = NpgsqlActivityListener.Value;

    private static ActivityListener CreateNpgsqlActivityListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source =>
                source.Name.Equals(NpgsqlActivitySourceName, StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> options) =>
                SampleNpgsqlActivity(options.Name),
            SampleUsingParentId = static (ref ActivityCreationOptions<string> options) =>
                SampleNpgsqlActivity(options.Name),
            ActivityStarted = static activity =>
            {
                var state = CurrentRequestState.Value;
                if (state is { IsActive: true })
                {
                    activity.SetCustomProperty(RequestStateActivityPropertyName, state);
                }
            },
            ActivityStopped = static activity =>
                (activity.GetCustomProperty(RequestStateActivityPropertyName) as RequestTimingState)?
                    .AddDatabaseDuration(activity.Duration)
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static ActivitySamplingResult SampleNpgsqlActivity(string activityName)
    {
        var state = CurrentRequestState.Value;
        if (state is null
            || !state.IsActive
            || activityName.StartsWith(NpgsqlConnectionActivityPrefix, StringComparison.Ordinal))
        {
            return ActivitySamplingResult.None;
        }

        return ActivitySamplingResult.PropagationData;
    }

    private static void ClearCurrentRequestState(RequestTimingState state)
    {
        if (ReferenceEquals(CurrentRequestState.Value, state))
        {
            CurrentRequestState.Value = null;
        }
    }

    private sealed class RequestTimingState(long startedAt)
    {
        private readonly object _gate = new();
        private long _databaseTicks;
        private long _databaseOperationCount;
        private bool _active = true;

        public long StartedAt { get; } = startedAt;

        public bool IsActive
        {
            get
            {
                lock (_gate)
                {
                    return _active;
                }
            }
        }

        public void AddDatabaseDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                return;
            }

            lock (_gate)
            {
                if (!_active)
                {
                    return;
                }

                _databaseTicks += duration.Ticks;
                _databaseOperationCount++;
            }
        }

        public DatabaseTimingSnapshot CompleteAndSnapshot()
        {
            lock (_gate)
            {
                _active = false;
                return new DatabaseTimingSnapshot(_databaseTicks, _databaseOperationCount);
            }
        }

        public void Complete()
        {
            lock (_gate)
            {
                _active = false;
            }
        }
    }

    private readonly record struct DatabaseTimingSnapshot(long Ticks, long OperationCount);
}
