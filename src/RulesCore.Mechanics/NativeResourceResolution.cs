namespace RulesCore.Mechanics;

public enum NativeRecoveryEvent { ShortRest, LongRest }
public sealed record NativeResourceChange(string Status, string ResourceId, int? Current,
    int? Maximum, string? Reason = null);

/// <summary>Resource transitions are calculated, never persisted by Rules Core.</summary>
public static class NativeResourceResolution
{
    public static NativeResourceChange Spend(NativeResource resource, int current, int amount)
    {
        if (resource.Maximum < 0 || current < 0 || current > resource.Maximum || amount <= 0)
            return new("invalid-input", resource.Id, null, null);
        if (amount > current) return new("insufficient-resource", resource.Id, current, resource.Maximum);
        return new("resolved", resource.Id, current - amount, resource.Maximum);
    }

    public static NativeResourceChange Recover(NativeResource resource, int current, NativeRecoveryEvent rest)
    {
        if (resource.Maximum < 0 || current < 0 || current > resource.Maximum)
            return new("invalid-input", resource.Id, null, null);
        if (resource.Recovery == "unsupported")
            return new("unsupported", resource.Id, current, resource.Maximum, "No native recovery rule.");
        if (resource.Recovery == "long-rest" && rest == NativeRecoveryEvent.ShortRest)
            return new("resolved", resource.Id, current, resource.Maximum);
        if (resource.Recovery is "long-rest" or "short-or-long-rest")
            return new("resolved", resource.Id, resource.Maximum, resource.Maximum);
        return new("unsupported", resource.Id, current, resource.Maximum, "Unknown recovery schedule.");
    }
}
