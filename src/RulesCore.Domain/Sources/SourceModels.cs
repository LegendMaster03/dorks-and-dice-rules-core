namespace RulesCore.Domain.Sources;

public sealed class SourcePackage
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? License { get; set; }
    public bool IsPublic { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<SourceRepresentation> Representations { get; set; } = new List<SourceRepresentation>();
    public ICollection<SourceEntity> Entities { get; set; } = new List<SourceEntity>();
    public ICollection<UserSourceGrant> UserGrants { get; set; } = new List<UserSourceGrant>();
}

public sealed class SourceRepresentation
{
    public Guid Id { get; set; }
    public Guid SourcePackageId { get; set; }
    public Guid? PreviousSourceRepresentationId { get; set; }
    public string FormatKey { get; set; } = string.Empty;
    public string OriginIdentity { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? SourceUri { get; set; }
    public string? MediaType { get; set; }
    public string ContentSha256 { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public byte[] ContentBytes { get; set; } = Array.Empty<byte>();
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset ImportedAt { get; set; }

    public SourcePackage SourcePackage { get; set; } = null!;
    public SourceRepresentation? PreviousSourceRepresentation { get; set; }
    public ICollection<SourceRepresentation> SupersedingRepresentations { get; set; } = new List<SourceRepresentation>();
    public ICollection<SourceEntityRevision> EntityRevisions { get; set; } = new List<SourceEntityRevision>();
}

public sealed class SourceEntity
{
    public Guid Id { get; set; }
    public Guid SourcePackageId { get; set; }
    public string FormatKey { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SourceCode { get; set; }
    public string NativeKey { get; set; } = string.Empty;
    public string NativeIdentityJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }

    public SourcePackage SourcePackage { get; set; } = null!;
    public ICollection<SourceEntityRevision> Revisions { get; set; } = new List<SourceEntityRevision>();
}

public sealed class SourceEntityRevision
{
    public Guid Id { get; set; }
    public Guid SourceEntityId { get; set; }
    public Guid SourceRepresentationId { get; set; }
    public int RevisionNumber { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public string? LocatorKey { get; set; }
    public DateTimeOffset ImportedAt { get; set; }

    public SourceEntity SourceEntity { get; set; } = null!;
    public SourceRepresentation SourceRepresentation { get; set; } = null!;
}

public sealed class UserSourceGrant
{
    public Guid Id { get; set; }
    public Guid SourcePackageId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset GrantedAt { get; set; }

    public SourcePackage SourcePackage { get; set; } = null!;
}
