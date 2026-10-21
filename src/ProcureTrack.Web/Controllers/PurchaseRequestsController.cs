using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProcureTrack.Web.Data;
using ProcureTrack.Web.Models;
using ProcureTrack.Web.Security;
using ProcureTrack.Web.Services;
using ProcureTrack.Web.ViewModels;

namespace ProcureTrack.Web.Controllers;

// No class-level [Authorize] needed: the fallback policy in Program.cs already requires a signed-in user.
public class PurchaseRequestsController : Controller
{
    private readonly ProcureTrackDbContext _db;
    private readonly ApprovalWorkflow _workflow;
    private readonly TimeProvider _time;

    public PurchaseRequestsController(ProcureTrackDbContext db, ApprovalWorkflow workflow, TimeProvider time)
    {
        _db = db;
        _workflow = workflow;
        _time = time;
    }

    // ---------- List (any signed-in user) ----------

    // GET /PurchaseRequests?Filter.DepartmentId=3&Filter.Status=Pending ...
    public async Task<IActionResult> Index(PurchaseRequestFilter filter)
    {
        if (filter.SubmittedFrom > filter.SubmittedTo)
        {
            ModelState.AddModelError("Filter.SubmittedTo", "The end date must be on or after the start date.");
        }

        // IQueryable builds the SQL lazily; every value below becomes a SQL parameter.
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
        };

        // jQuery sends "X-Requested-With: XMLHttpRequest" on AJAX calls; return just the results table.
        if (Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            return PartialView("_RequestTable", model);
        }

        model.Departments = await DepartmentOptionsAsync();
        model.Vendors = await _db.Vendors.AsNoTracking()
            .OrderBy(v => v.Name)
            .Select(v => new SelectListItem(v.Name, v.Id.ToString()))
            .ToListAsync();

        return View(model);
    }

    // ---------- Details (any signed-in user) ----------

    // GET /PurchaseRequests/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var model = await BuildDetailsViewModelAsync(id, decision: null);
        return model is null ? NotFound() : View(model);
    }

    // ---------- Decide (Approvers only) ----------

    // POST /PurchaseRequests/Decide/5
    [HttpPost]
    [Authorize(Roles = AppRoles.Approver)]
    public async Task<IActionResult> Decide(int id, [Bind(Prefix = "Decision")] DecisionViewModel decision)
    {
        var request = await _db.PurchaseRequests.FirstOrDefaultAsync(p => p.Id == id);

        if (request is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return await RedisplayDetailsAsync(id, decision);
        }

        var action = decision.Action!.Value;

        // Guard against deciding on a version the approver never saw.
        _db.Entry(request).Property(p => p.RowVersion).OriginalValue = decision.RowVersion ?? [];

        // The approver is the signed-in user, never a value from the form.
        // ApprovalWorkflow enforces every rule, including the required rejection reason,
        // regardless of what the browser-side jQuery checks did or did not do.
        var result = action == ApprovalAction.Approved
            ? _workflow.Approve(request, CurrentUserName, decision.Notes)
            : _workflow.Reject(request, CurrentUserName, decision.Notes);

        if (!result.Succeeded)
        {
            // Already decided by someone else: the form will not be shown, so use a page-level message.
            if (!_workflow.CanDecide(request))
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
            return await RedisplayDetailsAsync(id, decision);
        }

        try
        {
            // One SaveChanges = one transaction: the status update and the audit record succeed or fail together.
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] =
                $"{request.RequestNumber} was changed by someone else before your decision was saved. " +
                "Review the current version and try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = $"{request.RequestNumber} was {action.ToString().ToLowerInvariant()}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Create (Requesters only) ----------

    // GET /PurchaseRequests/Create
    [Authorize(Roles = AppRoles.Requester)]
    public async Task<IActionResult> Create()
    {
        var model = new PurchaseRequestFormViewModel();
        await PopulateOptionsAsync(model);
        return View(model);
    }

    // POST /PurchaseRequests/Create
    [HttpPost]
    [Authorize(Roles = AppRoles.Requester)]
    public async Task<IActionResult> Create(PurchaseRequestFormViewModel model)
    {
        var now = _time.GetUtcNow().UtcDateTime;

        await ValidateBusinessRulesAsync(model, earliestRequiredBy: DateOnly.FromDateTime(now));

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model);
            return View(model);
        }

        // Validation passed, so the nullable values are known to be present ('!' and '.Value' assert that).
        var request = new PurchaseRequest
        {
            Title = model.Title!.Trim(),
            Description = Clean(model.Description),
            DepartmentId = model.DepartmentId!.Value,
            VendorId = model.VendorId!.Value,
            Amount = model.Amount!.Value,
            RequiredByDate = model.RequiredByDate!.Value,
            BusinessJustification = model.BusinessJustification!.Trim(),
            RequestedBy = CurrentUserName,      // from the signed-in identity, not the form
            SubmittedDate = now,                // set by the server, never by the form
            Status = RequestStatus.Pending,     // every new request starts Pending
        };

        _db.PurchaseRequests.Add(request);
        await _db.SaveChangesAsync();

        // Post/Redirect/Get: refreshing the next page will not resubmit the form.
        TempData["Success"] = $"{request.RequestNumber} was submitted for review.";
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    // ---------- Edit (the Requester who owns it, while Pending) ----------

    // GET /PurchaseRequests/Edit/5
    [Authorize(Roles = AppRoles.Requester)]
    public async Task<IActionResult> Edit(int id)
    {
        var request = await _db.PurchaseRequests.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

        if (request is null)
        {
            return NotFound();
        }

        if (!IsOwner(request))
        {
            return Forbid();
        }

        if (!_workflow.CanEdit(request))
        {
            TempData["Error"] = $"{request.RequestNumber} is {request.Status} and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new PurchaseRequestFormViewModel
        {
            Id = request.Id,
            RequestNumber = request.RequestNumber,
            Title = request.Title,
            Description = request.Description,
            DepartmentId = request.DepartmentId,
            VendorId = request.VendorId,
            Amount = request.Amount,
            RequiredByDate = request.RequiredByDate,
            BusinessJustification = request.BusinessJustification,
            RowVersion = request.RowVersion,
        };

        await PopulateOptionsAsync(model);
        return View(model);
    }

    // POST /PurchaseRequests/Edit/5
    [HttpPost]
    [Authorize(Roles = AppRoles.Requester)]
    public async Task<IActionResult> Edit(int id, PurchaseRequestFormViewModel model)
    {
        var request = await _db.PurchaseRequests.FirstOrDefaultAsync(p => p.Id == id);

        if (request is null)
        {
            return NotFound();
        }

        if (!IsOwner(request))
        {
            return Forbid();
        }

        // Re-checked on POST: hiding the Edit button is not enforcement; this is.
        if (!_workflow.CanEdit(request))
        {
            TempData["Error"] = $"{request.RequestNumber} is {request.Status} and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await ValidateBusinessRulesAsync(model, earliestRequiredBy: DateOnly.FromDateTime(request.SubmittedDate));

        if (!ModelState.IsValid)
        {
            model.Id = request.Id;
            model.RequestNumber = request.RequestNumber;
            await PopulateOptionsAsync(model);
            return View(model);
        }

        // SaveChanges issues "UPDATE ... WHERE Id = @id AND RowVersion = @original";
        // if another save happened first, EF throws DbUpdateConcurrencyException instead of overwriting.
        _db.Entry(request).Property(p => p.RowVersion).OriginalValue = model.RowVersion ?? [];

        request.Title = model.Title!.Trim();
        request.Description = Clean(model.Description);
        request.DepartmentId = model.DepartmentId!.Value;
        request.VendorId = model.VendorId!.Value;
        request.Amount = model.Amount!.Value;
        request.RequiredByDate = model.RequiredByDate!.Value;
        request.BusinessJustification = model.BusinessJustification!.Trim();

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty,
                "Someone else changed this request after you opened it. " +
                "Reload the page to see the latest version, then reapply your changes.");
            model.Id = request.Id;
            model.RequestNumber = request.RequestNumber;
            await PopulateOptionsAsync(model);
            return View(model);
        }

        TempData["Success"] = $"{request.RequestNumber} was updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Delete (the Requester who owns it, while Pending) ----------

    // GET /PurchaseRequests/Delete/5 (confirmation page)
    [Authorize(Roles = AppRoles.Requester)]
    public async Task<IActionResult> Delete(int id)
    {
        var request = await _db.PurchaseRequests
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.Vendor)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (request is null)
        {
            return NotFound();
        }

        if (!IsOwner(request))
        {
            return Forbid();
        }

        if (!_workflow.CanDelete(request))
        {
            TempData["Error"] = $"{request.RequestNumber} is {request.Status} and cannot be deleted; it is part of the audit trail.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(request);
    }

    // POST /PurchaseRequests/Delete/5
    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = AppRoles.Requester)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var request = await _db.PurchaseRequests.FirstOrDefaultAsync(p => p.Id == id);

        if (request is null)
        {
            TempData["Error"] = "That request no longer exists.";
            return RedirectToAction(nameof(Index));
        }

        if (!IsOwner(request))
        {
            return Forbid();
        }

        if (!_workflow.CanDelete(request))
        {
            TempData["Error"] = $"{request.RequestNumber} is {request.Status} and cannot be deleted; it is part of the audit trail.";
            return RedirectToAction(nameof(Details), new { id });
        }

        _db.PurchaseRequests.Remove(request);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{request.RequestNumber} was deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------

    /// <summary>The signed-in user's display name. The fallback policy guarantees someone is signed in.</summary>
    private string CurrentUserName =>
        User.Identity?.Name ?? throw new InvalidOperationException("No signed-in user.");

    /// <summary>
    /// Ownership by display name, because the schema stores RequestedBy as a name.
    /// A production system would store and compare a stable user ID (documented limitation).
    /// </summary>
    private bool IsOwner(PurchaseRequest request) =>
        string.Equals(request.RequestedBy.Trim(), CurrentUserName, StringComparison.OrdinalIgnoreCase);

    private async Task<IActionResult> RedisplayDetailsAsync(int id, DecisionViewModel decision)
    {
        var model = await BuildDetailsViewModelAsync(id, decision);
        return model is null ? NotFound() : View(nameof(Details), model);
    }

    private async Task<PurchaseRequestDetailsViewModel?> BuildDetailsViewModelAsync(int id, DecisionViewModel? decision)
    {
        var request = await _db.PurchaseRequests
            .AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.Vendor)
            .Include(p => p.ApprovalRecords)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (request is null)
        {
            return null;
        }

        var isOwner = IsOwner(request);

        return new PurchaseRequestDetailsViewModel
        {
            Request = request,
            IsOwnRequest = isOwner,
            CanEdit = _workflow.CanEdit(request) && isOwner && User.IsInRole(AppRoles.Requester),
            CanDecide = _workflow.CanDecide(request) && User.IsInRole(AppRoles.Approver) && !isOwner,
            // Fresh page: start from the current version. Redisplay: keep the user's input and original version.
            Decision = decision ?? new DecisionViewModel { RowVersion = request.RowVersion },
        };
    }

    /// <summary>Rules that need the database or the clock, so attributes alone cannot express them.</summary>
    private async Task ValidateBusinessRulesAsync(PurchaseRequestFormViewModel model, DateOnly earliestRequiredBy)
    {
        if (model.Amount is decimal amount && decimal.Round(amount, 2) != amount)
        {
            ModelState.AddModelError(nameof(model.Amount), "Amount cannot have more than two decimal places.");
        }

        if (model.RequiredByDate is DateOnly requiredBy && requiredBy < earliestRequiredBy)
        {
            ModelState.AddModelError(nameof(model.RequiredByDate),
                $"Required-by date cannot be earlier than {earliestRequiredBy:MMM d, yyyy}.");
        }

        // Never trust posted IDs: confirm they exist (and the vendor is active).
        if (model.DepartmentId is int departmentId && !await _db.Departments.AnyAsync(d => d.Id == departmentId))
        {
            ModelState.AddModelError(nameof(model.DepartmentId), "Select a valid department.");
        }

        if (model.VendorId is int vendorId && !await _db.Vendors.AnyAsync(v => v.Id == vendorId && v.IsActive))
        {
            ModelState.AddModelError(nameof(model.VendorId), "Select an active vendor.");
        }
    }

    private async Task PopulateOptionsAsync(PurchaseRequestFormViewModel model)
    {
        model.Departments = await DepartmentOptionsAsync();

        // New and edited requests may only use active vendors.
        model.Vendors = await _db.Vendors.AsNoTracking()
            .Where(v => v.IsActive)
            .OrderBy(v => v.Name)
            .Select(v => new SelectListItem(v.Name, v.Id.ToString()))
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> DepartmentOptionsAsync() =>
        await _db.Departments.AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
            .ToListAsync();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}