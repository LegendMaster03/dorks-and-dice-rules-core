using System.Text.Json;

namespace RulesCore.Mechanics;

internal static class NativeOriginUnlockLevel
{
    public static int? Read(JsonElement trait)
    {
        var required = ReadField("requiredCharacterLevel");
        if (!trait.TryGetProperty("spell", out _) && !trait.TryGetProperty("spellList", out _))
            return required;

        var spellLevel = ReadField("level");
        if (required is null || spellLevel is null ||
            trait.TryGetProperty("requiredCharacterLevel", out _) &&
            trait.TryGetProperty("level", out _) && required != spellLevel)
            return null;
        return Math.Max(required.Value, spellLevel.Value);

        int? ReadField(string field) => !trait.TryGetProperty(field, out var value)
            ? 1
            : value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var level) && level is >= 1 and <= 1000
                ? level
                : null;
    }
}
