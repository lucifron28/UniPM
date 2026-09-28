namespace UniPM.Api.Models;

public sealed class InspectionLocationAttempt
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset? DevicePositionTimestamp { get; set; }
    public double MeasuredLatitude { get; set; }
    public double MeasuredLongitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public bool HasAccuracy { get; set; } = true;
    public bool IsMocked { get; set; }
    public string AccuracyMode { get; set; } = "Unknown";
    public int AcquisitionDurationMs { get; set; }
    public double? ExpectedLatitude { get; set; }
    public double? ExpectedLongitude { get; set; }
    public double? ExpectedRadiusMeters { get; set; }
    public double? DistanceMeters { get; set; }
    public string Outcome { get; set; } = string.Empty;
}
