using Microsoft.EntityFrameworkCore;
using ProcureTrack.Web.Models;

namespace ProcureTrack.Web.Data;

/// <summary>
/// Inserts fictional sample data into an empty database. Development use only.
/// Vendor names use Microsoft's standard fictional company names.
/// </summary>
public static class DbSeeder
{
    private const string Approver = "Morgan Alvarez";

    private sealed record SeedRequest(
        string Title,
        string DepartmentCode,
        string VendorName,
        decimal Amount,
        int DaysAgo,
        int NeededInDays,
        RequestStatus Status,
        string RequestedBy,
        string Justification,
        string? DecisionNotes = null);
    
    public static async Task SeedAsync(ProcureTrackDbContext db)
    {
        // Idempotent: do nothing if data already exists, so restarts never duplicate data.
        if (await db.Departments.AnyAsync())
        {
            return;
        }

        Department[] departments =
        [
            new() { Name = "Accounting", Code = "ACCT" },
            new() { Name = "Human Resources", Code = "HR" },
            new() { Name = "Procurement", Code = "PROC" },
            new() { Name = "Project Controls", Code = "PC" },
            new() { Name = "Information Technology", Code = "IT" },
            new() { Name = "Facilities", Code = "FAC" },
        ];

        Vendor[] vendors =
        [
            new() { Name = "Contoso Office Supply", ContactEmail = "orders@contoso.example" },
            new() { Name = "Fabrikam Safety Equipment", ContactEmail = "sales@fabrikam.example" },
            new() { Name = "Northwind IT Services", ContactEmail = "quotes@northwind.example" },
            new() { Name = "Tailspin Lab Instruments", ContactEmail = "support@tailspin.example" },
            new() { Name = "Woodgrove Facilities Services", ContactEmail = "contracts@woodgrove.example" },
            new() { Name = "Litware Training Group", ContactEmail = "training@litware.example" },
            new() { Name = "Proseware Software Licensing", ContactEmail = "licensing@proseware.example" },
            new() { Name = "Adventure Works Furniture", ContactEmail = "orders@adventureworks.example" }
        ];

        db.Departments.AddRange(departments);
        db.Vendors.AddRange(vendors);

        SeedRequest[] seeds =
        [
            new("Ergonomic office chairs (12)", "FAC", "Adventure Works Furniture", 4188.00m, 41, 30, RequestStatus.Approved,
                "Dana Whitfield", "Replace damaged chairs in the second-floor open office.", "Within the facilities furniture budget."),
            new("Annual accounting software license renewal", "ACCT", "Proseware Software Licensing", 18500.00m, 38, 21, RequestStatus.Approved,
                "Marcus Bell", "Current license expires at fiscal year end; renewal avoids loss of access.", "Approved at the negotiated renewal rate."),
            new("Safety glasses and gloves restock", "FAC", "Fabrikam Safety Equipment", 1265.40m, 35, 14, RequestStatus.Approved,
                "Luis Ortega", "Quarterly PPE restock for maintenance crews."),
            new("Laptop refresh for new analysts (4)", "IT", "Northwind IT Services", 7196.00m, 33, 20, RequestStatus.Approved,
                "Hannah Cho", "Equipment for four analysts starting next month.", "Standard configuration confirmed with IT."),
            new("Benefits enrollment training workshop", "HR", "Litware Training Group", 3200.00m, 30, 25, RequestStatus.Rejected,
                "Renee Patterson", "Train HR staff on the new benefits portal.",
                "Covered under the existing training contract. Resubmit only if additional sessions are needed."),
            new("Copier paper and toner (quarterly)", "ACCT", "Contoso Office Supply", 642.75m, 28, 10, RequestStatus.Approved,
                "Marcus Bell", "Routine quarterly office supply order."),
            new("Scheduling software add-on module", "PC", "Proseware Software Licensing", 9400.00m, 27, 30, RequestStatus.Rejected,
                "Owen Gallagher", "Resource-loading module for project schedules.",
                "Duplicates a module funded last fiscal year. Coordinate with IT before resubmitting."),
            new("Network switch replacement", "IT", "Northwind IT Services", 5830.00m, 25, 14, RequestStatus.Approved,
                "Hannah Cho", "Failing core switch in Building B is causing intermittent outages.", "Priority approval due to outage risk."),
            new("Calibration of field measurement instruments", "FAC", "Tailspin Lab Instruments", 2750.00m, 22, 30, RequestStatus.Approved,
                "Luis Ortega", "Annual calibration required to keep instruments in service."),
            new("HVAC filter replacement contract", "FAC", "Woodgrove Facilities Services", 12400.00m, 20, 45, RequestStatus.Approved,
                "Dana Whitfield", "Twelve-month filter replacement service for all office buildings.", "Approved for a twelve-month term."),
            new("Recruiting fair booth materials", "HR", "Contoso Office Supply", 875.20m, 18, 21, RequestStatus.Rejected,
                "Renee Patterson", "Booth signage and handouts for the spring career fair.",
                "The career fair was cancelled; no purchase is needed."),
            new("Procurement policy training for buyers", "PROC", "Litware Training Group", 4500.00m, 16, 30, RequestStatus.Approved,
                "Tomás Reyes", "Refresher training on updated purchasing thresholds."),
            new("Standing desks for Project Controls (6)", "PC", "Adventure Works Furniture", 3894.00m, 14, 30, RequestStatus.Rejected,
                "Owen Gallagher", "Ergonomic accommodation and team request.",
                "Vendor is no longer active. Resubmit using an approved furniture vendor."),
            new("Contract management system license", "PROC", "Proseware Software Licensing", 21750.00m, 12, 30, RequestStatus.Approved,
                "Tomás Reyes", "Centralize contract tracking currently managed in spreadsheets.", "Approved; implementation to follow IT review."),
            new("Monitor upgrades for analysts (8)", "ACCT", "Northwind IT Services", 2392.00m, 10, 21, RequestStatus.Pending,
                "Marcus Bell", "Dual monitors to support month-end close reconciliation."),
            new("Fall-protection harness inspection", "FAC", "Fabrikam Safety Equipment", 1980.00m, 9, 14, RequestStatus.Pending,
                "Luis Ortega", "Required periodic inspection of fall-protection equipment."),
            new("Onboarding supplies for new hires", "HR", "Contoso Office Supply", 455.60m, 8, 14, RequestStatus.Pending,
                "Renee Patterson", "Desk supplies for six incoming employees."),
            new("Earned value management training", "PC", "Litware Training Group", 6200.00m, 7, 45, RequestStatus.Pending,
                "Owen Gallagher", "Build team capability in earned value reporting."),
            new("Environmental sampling kits", "FAC", "Tailspin Lab Instruments", 3475.00m, 6, 30, RequestStatus.Pending,
                "Dana Whitfield", "Replace expired sampling kits used for routine air quality checks."),
            new("Backup storage expansion", "IT", "Northwind IT Services", 14250.00m, 5, 30, RequestStatus.Pending,
                "Hannah Cho", "Backup storage is at 85% capacity and growing."),
            new("Purchase order form printing", "PROC", "Contoso Office Supply", 318.40m, 4, 10, RequestStatus.Pending,
                "Tomás Reyes", "Printed forms for field locations without system access."),
            new("Payroll compliance webinar series", "HR", "Litware Training Group", 1150.00m, 3, 20, RequestStatus.Pending,
                "Renee Patterson", "Annual payroll regulation update training."),
            new("Janitorial services contract extension", "FAC", "Woodgrove Facilities Services", 28600.00m, 2, 30, RequestStatus.Pending,
                "Dana Whitfield", "Current janitorial contract ends next month."),
            new("Audit software license (additional seats)", "ACCT", "Proseware Software Licensing", 5400.00m, 1, 14, RequestStatus.Pending,
                "Marcus Bell", "Three additional seats for the internal audit team."),
        ];

        var now = DateTime.UtcNow;

        foreach (var seed in seeds)
        {
            var submitted = now.AddDays(-seed.DaysAgo);

            var request = new PurchaseRequest
            {
                Title = seed.Title,
                Department = departments.Single(d => d.Code == seed.DepartmentCode),
                Vendor = vendors.Single(v => v.Name == seed.VendorName),
                Amount = seed.Amount,
                BusinessJustification = seed.Justification,
                RequestedBy = seed.RequestedBy,
                SubmittedDate = submitted,
                RequiredByDate = DateOnly.FromDateTime(submitted.AddDays(seed.NeededInDays)),
                Status = seed.Status
            };

            // Decided requests get a matching audit record
            if (seed.Status != RequestStatus.Pending)
            {
                request.ApprovalRecords.Add(new ApprovalRecord
                {
                    Action = seed.Status == RequestStatus.Approved ? ApprovalAction.Approved : ApprovalAction.Rejected,
                    Notes = seed.DecisionNotes ?? string.Empty,
                    ActedBy = Approver,
                    ActedAt = submitted.AddDays(2),
                });
            }

            db.PurchaseRequests.Add(request);
        }
        // One SaveChanges call wraps every insert in a single transaction, so either all succeed or all fail.
        await db.SaveChangesAsync();
    }
}