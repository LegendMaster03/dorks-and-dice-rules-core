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

    public ICollection<SourceWork> Works { get; set; } = new List<SourceWork>();
}

public sealed class SourceWork
{
    public Guid Id { get; set; }
    public Guid SourcePackageId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public SourcePackage SourcePackage { get; set; } = null!;
    public ICollection<SourceEdition> Editions { get; set; } = new List<SourceEdition>();
}

public sealed class SourceEdition
{
    public Guid Id { get; set; }
    public Guid SourceWorkId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public SourceWork SourceWork { get; set; } = null!;
    public ICollection<SourceEntity> Entities { get; set; } = new List<SourceEntity>();
}

public sealed class SourceEntity
{
    public Guid Id { get; set; }
    public Guid SourceEditionId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string NaturalKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public SourceEdition SourceEdition { get; set; } = null!;
    public ICollection<SourceEntityRevision> Revisions { get; set; } = new List<SourceEntityRevision>();
}

public sealed class SourceEntityRevision
{
    public Guid Id { get; set; }
    public Guid SourceEntityId { get; set; }
    public int RevisionNumber { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; set; }

    public SourceEntity SourceEntity { get; set; } = null!;
}
