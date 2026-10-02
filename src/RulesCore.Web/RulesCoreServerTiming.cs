using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;

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

    private static readonly object RequestStateKey = new();

    public static void EnsureRequestTiming(HttpContext httpContext)
    {
        if (httpContext.Items.ContainsKey(RequestStateKey))
        {
            return;
        }

        var state = new RequestTimingState(Stopwatch.GetTimestamp());
        httpContext.Items[RequestStateKey] = state;

        httpContext.Response.OnStarting(() =>
        {
            var databaseTicks = state.DatabaseTicks;
            if (databaseTicks > 0)
            {
                AppendDuration(
                    httpContext,
                    DatabaseMetricName,
                    TimeSpan.FromTicks(databaseTicks).TotalMilliseconds);
            }

            AppendDuration(
                httpContext,
                RequestMetricName,
                Stopwatch.GetElapsedTime(state.StartedAt).TotalMilliseconds);
            return Task.CompletedTask;
        });
    }

    public static void AddDatabaseDuration(HttpContext? httpContext, TimeSpan duration)
    {
        if (httpContext is null
            || !httpContext.Items.TryGetValue(RequestStateKey, out var value)
            || value is not RequestTimingState state)
        {
            return;
        }

        state.AddDatabaseTicks(duration.Ticks);
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

    private sealed class RequestTimingState(long startedAt)
    {
        private long _databaseTicks;

        public long StartedAt { get; } = startedAt;
        public long DatabaseTicks => Interlocked.Read(ref _databaseTicks);

        public void AddDatabaseTicks(long ticks)
        {
            if (ticks > 0)
            {
                Interlocked.Add(ref _databaseTicks, ticks);
            }
        }
    }
}

public sealed class RulesCoreDbCommandTimingInterceptor(IHttpContextAccessor httpContextAccessor)
    : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Record(eventData.Duration);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Record(eventData.Duration);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Record(eventData.Duration);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        Record(eventData.Duration);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration);
        return Task.CompletedTask;
    }

    private void Record(TimeSpan duration) =>
        RulesCoreServerTiming.AddDatabaseDuration(
            httpContextAccessor.HttpContext,
            duration);
}
