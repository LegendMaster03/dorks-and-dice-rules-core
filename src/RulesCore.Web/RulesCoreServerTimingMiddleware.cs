namespace RulesCore.Web;

public sealed class RulesCoreServerTimingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        RulesCoreServerTiming.EnsureRequestTiming(httpContext);
        await next(httpContext);
    }
}
