namespace RulesCore.Application.Sources;

public sealed record CanonicalSourceAssociationView(
    CanonicalPublicationIdentityView Publication,
    Guid CanonicalOccurrenceId,
    string PublicationMatchKind,
    string OccurrenceMatchKind,
    double Confidence);
