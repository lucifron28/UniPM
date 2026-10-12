namespace UniPM.Api.Models;

public sealed class InspectionPhotoEvidence
{
    public Guid InspectionId { get; set; }
    public InspectionRecord? Inspection { get; set; }

    public string StorageKey { get; set; } = string.Empty;
    public int LengthBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public int Revision { get; set; } = 1;
}
