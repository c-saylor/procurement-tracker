using System.ComponentModel.DataAnnotations;
using ProcureTrack.Web.Models;

namespace ProcureTrack.Web.ViewModels;

/// <summary>Filter criteria bound from the query string, so filtered views can be bookmarked and shared.</summary>
public class PurchaseRequestFilter
{
    [Display(Name = "Search")]
    [StringLength(100)]
    public string? Search { get; set; }

    [Display(Name = "Department")]
    public int? DepartmentId { get; set; }

    [Display(Name = "Vendor")]
    public int? VendorId { get; set; }

    [Display(Name = "Status")]
    public RequestStatus? Status { get; set; }

    [Display(Name = "Submitted from")]
    public DateOnly? SubmittedFrom { get; set; }

    [Display(Name = "Submitted to")]
    public DateOnly? SubmittedTo { get; set; }
}