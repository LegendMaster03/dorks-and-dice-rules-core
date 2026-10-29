namespace RulesCore.Content;

/// <summary>
/// Safe machine-readable code for readiness; the detailed message is for
/// internal validation and tests and must not be returned through HTTP.
/// </summary>
public sealed class NativeCatalogLoadException : Exception
{
    public string ReasonCode { get; }

    public NativeCatalogLoadException(
        string reasonCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ReasonCode = reasonCode;
    }
}
