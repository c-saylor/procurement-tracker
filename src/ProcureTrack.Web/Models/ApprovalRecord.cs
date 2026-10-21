using System.ComponentModel.DataAnnotations;

namespace ProcureTrack.Web.Models;

/// <summary>Append-only audit record of an approval decision. Never edited or deleted.</summary>
public class ApprovalRecord
{
    public int Id { get; set; }

    public int PurchaseRequestId { get; set; }
    public PurchaseRequest PurchaseRequest { get; set; } = null!;

    public ApprovalAction Action { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required, StringLength(100)]
    public string ActedBy { get; set; } = string.Empty;

    /// <summary>Set by the server (UTC) when the decision is recorded.</summary>
    public DateTime ActedAt { get; set; }
}