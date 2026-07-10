namespace UniPM.Api.Data.Seeding;

public sealed class SyntheticMaintenanceDataset
{
    public string DatasetVersion { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public SyntheticExpectedCounts ExpectedCounts { get; set; } = new();
    public List<SyntheticSourceBasis> SourceBasis { get; set; } = [];
    public List<string> Assumptions { get; set; } = [];
    public List<SyntheticActor> Actors { get; set; } = [];
    public List<SyntheticAsset> Assets { get; set; } = [];
    public List<SyntheticSchedule> Schedules { get; set; } = [];
    public List<SyntheticInspection> Inspections { get; set; } = [];
}

public sealed class SyntheticExpectedCounts
{
    public int Actors { get; set; }
    public int Assets { get; set; }
    public int Schedules { get; set; }
    public int Inspections { get; set; }
}

public sealed class SyntheticSourceBasis
{
    public string AssetCategory { get; set; } = string.Empty;
    public string FormTitle { get; set; } = string.Empty;
    public string PageObserved { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string EffectivityDate { get; set; } = string.Empty;
    public string? DocumentCode { get; set; }
}

public sealed class SyntheticActor
{
    public string SeedKey { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string RoleToken { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
}

public sealed class SyntheticAsset
{
    public string SeedKey { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public string? Building { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string QrCodeValue { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public SyntheticCategoryDetails CategoryDetails { get; set; } = new();
}

public sealed class SyntheticCategoryDetails
{
    public string? Type { get; set; }
    public string? Capacity { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? DeviceParticulars { get; set; }
    public string? DeviceType { get; set; }
    public DateOnly? DateInstalled { get; set; }
    public string? UnitType { get; set; }
    public string? StationType { get; set; }
    public List<string>? FilterConfiguration { get; set; }
    public bool? HasUvLight { get; set; }
}

public sealed class SyntheticSchedule
{
    public string SeedKey { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public DateOnly ScheduleDate { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public string? Quarter { get; set; }
    public string? Semester { get; set; }
    public int? Year { get; set; }
    public string? AcademicYear { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class SyntheticInspection
{
    public string SeedKey { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetCategory { get; set; } = string.Empty;
    public Guid InspectorUserId { get; set; }
    public DateTimeOffset DateInspected { get; set; }
    public bool IsOperational { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public string ActionsRecommendations { get; set; } = string.Empty;
    public SyntheticFormData FormData { get; set; } = new();
}

public sealed class SyntheticFormData
{
    public List<string>? WorkDone { get; set; }
    public string? RmrfNumber { get; set; }
    public DateOnly? DateAccomplished { get; set; }
    public string? AcknowledgedByRole { get; set; }
    public string? NotedByRole { get; set; }
}
