using RulesCore.Application.Rules;
using RulesCore.Domain.Rules;

namespace RulesCore.Infrastructure.Rules;

internal static class CharacterContextualHelpProjection
{
    public static CharacterContextualHelpView? For(string? topicKey)
    {
        var definition = KnownCharacterContextualHelp.FindByTopicKey(topicKey);
        return definition is null
            ? null
            : new CharacterContextualHelpView(
                definition.TopicKey,
                definition.DisplayName,
                definition.ShortText,
                definition.FullText,
                definition.Prominence);
    }
}
