namespace ProcureTrack.Web.ViewModels;

public class DashboardViewModel
{
    public int StalePendingDays { get; init; }

    public int TotalRequests { get; init; }
    public decimal TotalAmount { get; init; }

    public int PendingCount { get; init; }
    public decimal PendingAmount { get; init; }
    public int StalePendingCount { get; init; }

    public int ApprovedCount { get; init; }
    public decimal ApprovedAmount { get; init; }

    public int RejectedCount { get; init; }

    public IReadOnlyList<PurchaseRequestListItem> RecentRequests { get; init; } = [];

    public IReadOnlyList<DepartmentSpend> ApprovedByDepartment { get; init; } = [];
}

public class DepartmentSpend
{
    public string Department { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public decimal Amount { get; set; }
    public int PercentOfTotal { get; set; }
}