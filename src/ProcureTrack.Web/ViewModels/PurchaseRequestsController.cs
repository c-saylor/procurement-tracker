using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProcureTrack.Web.Data;
using ProcureTrack.Web.Services;
using ProcureTrack.Web.ViewModels;

namespace ProcureTrack.Web.Controllers;

public class PurchaseRequestsController : Controller
{
    private readonly ProcureTrackDbContext _db;
    private readonly ApprovalWorkflow _workflow;

    // Dependency injection: ASP.NET Core supplies these when it creates the controller for each request.
    public PurchaseRequestsController(ProcureTrackDbContext db, ApprovalWorkflow workflow)
    {
        _db = db;
        _workflow = workflow;
    }

    // GET /PurchaseRequests?Filter.DepartmentId=3&Filter.Status=Pending ...
    public async Task<IActionResult> Index(PurchaseRequestFilter filter)
    {
        if (filter.SubmittedFrom > filter.SubmittedTo)
        {
            ModelState.AddModelError("Filter.SubmittedTo", "The end date must be on or after the start date.");
        }

        // IQueryable builds the SQL lazily, like a Knex query builder. Nothing runs until ToListAsync().
        // Every value below becomes a SQL parameter, never concatenated text.
        var query = _db.PurchaseRequests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p => p.Title.Contains(term) || p.RequestedBy.Contains(term));
        }

        if (filter.DepartmentId is int departmentId)
        {
            query = query.Where(p => p.DepartmentId == departmentId);
        }

        if (filter.VendorId is int vendorId)
        {
            query = query.Where(p => p.VendorId == vendorId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (filter.SubmittedFrom is DateOnly from)
        {
            var fromStart = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.SubmittedDate >= fromStart);
        }

        if (filter.SubmittedTo is DateOnly to)
        {
            // Exclusive upper bound so the whole "to" day is included.
            var toExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.SubmittedDate < toExclusive);
        }

        var requests = await query
            .OrderByDescending(p => p.SubmittedDate)
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

        var model = new PurchaseRequestIndexViewModel
        {
            Filter = filter,
            Requests = requests,
            Departments = await _db.Departments.AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
                .ToListAsync(),
            Vendors = await _db.Vendors.AsNoTracking()
                .OrderBy(v => v.Name)
                .Select(v => new SelectListItem(v.Name, v.Id.ToString()))
                .ToListAsync(),
        };

        return View(model);
    }

    // GET /PurchaseRequests/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var request = await _db.PurchaseRequests
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.Vendor)
            .Include(p => p.ApprovalRecords)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (request is null)
        {
            return NotFound();
        }

        return View(new PurchaseRequestDetailsViewModel
        {
            Request = request,
            CanEdit = _workflow.CanEdit(request),
            CanDecide = _workflow.CanDecide(request),
        });
    }
}