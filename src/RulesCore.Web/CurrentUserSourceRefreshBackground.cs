using RulesCore.Application.Sources;
using RulesCore.Infrastructure.Persistence;
using RulesCore.Infrastructure.Sources;

namespace RulesCore.Web;

internal static class CurrentUserSourceRefreshBackground
{
    private static int started;

    public static void Start(WebApplication app)
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
        {
            return;
        }

        var stopping = app.Lifetime.ApplicationStopping;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(10), stopping);
                while (!stopping.IsCancellationRequested)
                {
                    try
                    {
                        await using var scope = app.Services.CreateAsyncScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<RulesCoreDbContext>();
                        var importer = scope.ServiceProvider.GetRequiredService<ISourceImportService>();
                        var grants = scope.ServiceProvider.GetRequiredService<ISourceGrantService>();
                        var refresh = new CurrentUserWebSourceRefreshService(
                            dbContext,
                            importer,
                            grants);
                        await refresh.RefreshDueAsync(stopping);

                        var identity = new CanonicalSourceIdentityService(dbContext);
                        await identity.IndexUnboundAsync(stopping);
                    }
                    catch (OperationCanceledException) when (stopping.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        app.Logger.LogError(
                            exception,
                            "Automatic Rules Core Web source refresh failed.");
                    }

                    await Task.Delay(TimeSpan.FromHours(1), stopping);
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                // Normal application shutdown.
            }
        }, stopping);
    }
}
