using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcureTrack.Web.Models;

public class PurchaseRequest
{
    public int Id { get; set; }
    
    [Required, StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;

    public decimal Amount { get; set; }

    [Required, StringLength(2000)]
    public string BusinessJustification { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>Set by the server (UTC) when the request is created. Never taken from form input.</summary>
    public DateTime SubmittedDate { get; set; }

    public DateOnly RequiredByDate { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    ///<summary>SQL Server rowversion used for optimistic concurrency.</summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public ICollection<ApprovalRecord> ApprovalRecords { get; set; } = new List<ApprovalRecord>();

    /// <summary>Display identifier such as PR-00042. Computed, not stored.</summary>
    [NotMapped]
    public string RequestNumber => FormatRequestNumber(Id);

    public static string FormatRequestNumber(int id) => $"PR-{id:D5}";
}