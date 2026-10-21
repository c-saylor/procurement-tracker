using Microsoft.AspNetCore.Mvc.Rendering;
using ProcureTrack.Web.Models;

namespace ProcureTrack.Web.ViewModels;

public class PurchaseRequestIndexViewModel
{
    public PurchaseRequestFilter Filter { get; set; } = new();

    public IReadOnlyList<PurchaseRequestListItem> Requests { get; set; } = [];

    public decimal TotalAmount => Requests.Sum(r => r.Amount);

    public IEnumerable<SelectListItem> Departments { get; set; } = [];

    public IEnumerable<SelectListItem> Vendors { get; set; } = [];

    public IEnumerable<SelectListItem> Statuses { get; } =
        Enum.GetValues<RequestStatus>().Select(s => new SelectListItem(s.ToString(), s.ToString()));
}

/// <summary>One table row. Contains only the columns the list needs.</summary>
public class PurchaseRequestListItem
{
    public int Id { get; set; }
    public string RequestNumber => PurchaseRequest.FormatRequestNumber(Id);
    public string Title { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime SubmittedDate { get; set; }
    public DateOnly RequiredByDate { get; set; }
    public RequestStatus Status { get; set; }
}