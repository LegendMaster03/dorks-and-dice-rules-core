using System.Text.Json;

namespace RulesCore.Application.Rules;

public sealed record WikiReferenceCompanionContentView(
    Guid CompanionContentId,
    string CompanionKind,
    string Name,
    string SourceCode,
    string PackageKey,
    string PackageDisplayName,
    Guid SourceRepresentationId,
    Guid SourceEntityId,
    Guid SourceEntityRevisionId,
    string AttachmentEvidenceKind,
    string ContentSha256,
    JsonElement Content);

public sealed record WikiReferenceCompanionContentCollectionView(
    string Scope,
    Guid? CampaignId,
    string ReferenceIdentity,
    IReadOnlyList<WikiReferenceCompanionContentView> Contents);
