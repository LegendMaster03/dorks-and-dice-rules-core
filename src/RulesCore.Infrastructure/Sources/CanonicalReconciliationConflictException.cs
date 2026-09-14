namespace RulesCore.Infrastructure.Sources;

internal sealed class CanonicalReconciliationConflictException : InvalidOperationException
{
    public CanonicalReconciliationConflictException(string message)
        : base(message)
    {
    }
}
