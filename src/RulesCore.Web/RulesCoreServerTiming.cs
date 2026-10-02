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
    public const string ReferenceDocumentsMetricName = "rules-core-reference-docs";
    public const string ReferenceTotalMetricName = "rules-core-reference-total";

    private const string NpgsqlActivitySourceName = "Npgsql";
    private const string NpgsqlConnectionActivityPrefix = "CONNECT ";

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
            if (state.DatabaseOperationCount > 0)
            {
                AppendDuration(
                    httpContext,
                    DatabaseMetricName,
                    TimeSpan.FromTicks(state.DatabaseTicks).TotalMilliseconds);
            }

            AppendDuration(
                httpContext,
                RequestMetricName,
                Stopwatch.GetElapsedTime(state.StartedAt).TotalMilliseconds);
            return Task.CompletedTask;
        });

        httpContext.Response.OnCompleted(() =>
        {
            state.Complete();
            if (ReferenceEquals(CurrentRequestState.Value, state))
            {
                CurrentRequestState.Value = null;
            }

            return Task.CompletedTask;
        });
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
            ActivityStopped = static activity =>
                CurrentRequestState.Value?.AddDatabaseDuration(activity.Duration)
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

    private sealed class RequestTimingState(long startedAt)
    {
        private long _databaseTicks;
        private long _databaseOperationCount;
        private int _active = 1;

        public long StartedAt { get; } = startedAt;
        public long DatabaseTicks => Interlocked.Read(ref _databaseTicks);
        public long DatabaseOperationCount => Interlocked.Read(ref _databaseOperationCount);
        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void AddDatabaseDuration(TimeSpan duration)
        {
            if (!IsActive || duration < TimeSpan.Zero)
            {
                return;
            }

            Interlocked.Add(ref _databaseTicks, duration.Ticks);
            Interlocked.Increment(ref _databaseOperationCount);
        }

        public void Complete() =>
            Interlocked.Exchange(ref _active, 0);
    }
}
