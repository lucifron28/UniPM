namespace UniPM.Api.Models;

/// <summary>
/// Stores the acknowledgement record associated with one preventive-maintenance form.
/// Signature data is optional so an authenticated acknowledgement can remain distinct
/// from a future signature-capture workflow.
/// </summary>
public sealed class PreventiveMaintenanceAcknowledgement
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public PreventiveMaintenanceForm? Form { get; set; }
    public string SignatoryName { get; set; } = string.Empty;
    public string SignatoryPosition { get; set; } = string.Empty;
    public string? SignatureData { get; set; }
    public string? SignatureContentType { get; set; }
    public string? SignatureChecksum { get; set; }
    public Guid CapturedByUserId { get; set; }
    public DateTimeOffset AcknowledgedAt { get; set; }
}
