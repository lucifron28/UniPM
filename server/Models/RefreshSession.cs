namespace UniPM.Api.Models;

public sealed class RefreshSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public Guid TokenFamilyId { get; set; }
    public string SecurityStampHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? LastUsedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public string? RevocationReason { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ApplicationUser User { get; set; } = null!;
    public RefreshSession? ReplacedBySession { get; set; }
}
