using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProcureTrack.Web.Data;
using ProcureTrack.Web.Models;
using ProcureTrack.Web.ViewModels;

namespace ProcureTrack.Web.Controllers;

public class HomeController : Controller
{
    private const int StalePendingDays = 7;

    private readonly ProcureTrackDbContext _db;
    private readonly TimeProvider _time;

    public HomeController(ProcureTrackDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    // GET / : the dashboard. Every figure is calculated from persisted records.
    public async Task<IActionResult> Index()
    {
        // One GROUP BY query returns the count and total amount for every status.
        var totals = await _db.PurchaseRequests
            .AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
            .ToListAsync();

        int CountFor(RequestStatus status) => totals.FirstOrDefault(t => t.Status == status)?.Count ?? 0;
        decimal AmountFor(RequestStatus status) => totals.FirstOrDefault(t => t.Status == status)?.Amount ?? 0m;

        var staleCutoff = _time.GetUtcNow().UtcDateTime.AddDays(-StalePendingDays);
        var stalePendingCount = await _db.PurchaseRequests
            .CountAsync(p => p.Status == RequestStatus.Pending && p.SubmittedDate < staleCutoff);

        var recentRequests = await _db.PurchaseRequests
            .AsNoTracking()
            .OrderByDescending(p => p.SubmittedDate)
            .Take(5)
            .Select(p => new PurchaseRequestListItem
            {
                Id = p.Id,
                Title = p.Title,
                DepartmentName = p.Department.Name,
                VendorName = p.Vendor.Name,
                Amount = p.Amount,
                SubmittedDate = p.SubmittedDate,
                RequiredByDate = p.RequiredByDate,
                Status = p.Status,
            })
            .ToListAsync();

        var approvedByDepartment = await _db.PurchaseRequests
            .AsNoTracking()
            .Where(p => p.Status == RequestStatus.Approved)
            .GroupBy(p => p.Department.Name)
            .OrderByDescending(g => g.Sum(p => p.Amount))
            .Select(g => new DepartmentSpend
            {
                Department = g.Key,
                RequestCount = g.Count(),
                Amount = g.Sum(p => p.Amount),
            })
            .ToListAsync();

        var approvedAmount = AmountFor(RequestStatus.Approved);
        foreach (var row in approvedByDepartment)
        {
            row.PercentOfTotal = approvedAmount == 0 ? 0 : (int)Math.Round(row.Amount / approvedAmount * 100);
        }

        var model = new DashboardViewModel
        {
            StalePendingDays = StalePendingDays,
            TotalRequests = totals.Sum(t => t.Count),
            TotalAmount = totals.Sum(t => t.Amount),
            PendingCount = CountFor(RequestStatus.Pending),
            PendingAmount = AmountFor(RequestStatus.Pending),
            StalePendingCount = stalePendingCount,
            ApprovedCount = CountFor(RequestStatus.Approved),
            ApprovedAmount = approvedAmount,
            RejectedCount = CountFor(RequestStatus.Rejected),
            RecentRequests = recentRequests,
            ApprovedByDepartment = approvedByDepartment,
        };

        return View(model);
    }

    // Reached via UseStatusCodePagesWithReExecute (for example, after NotFound() or a failed antiforgery check).
    // The response keeps its original status code; this only supplies a friendly body.
    [AllowAnonymous]
    public IActionResult HttpStatus(int code)
    {
        return View(code);
    }

    // Reached via UseExceptionHandler in Production. Shows a reference ID, never exception details.
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}