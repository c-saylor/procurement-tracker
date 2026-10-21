using Microsoft.EntityFrameworkCore;
using ProcureTrack.Web.Models;

namespace ProcureTrack.Web.Data;

public class ProcureTrackDbContext : DbContext
{
    public ProcureTrackDbContext(DbContextOptions<ProcureTrackDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseRequest> PurchaseRequests => Set<PurchaseRequest>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasIndex(d => d.Name).IsUnique();
            entity.HasIndex(d => d.Code).IsUnique();
        });

        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.HasIndex(v => v.Name).IsUnique();
        });

        modelBuilder.Entity<PurchaseRequest>(entity =>
        {
            entity.Property(p => p.Amount).HasPrecision(18, 2);

            // Store enum names ("Pending") rather than numbers so SQL and SSRS reports are readable.
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

            // Restrict: a department or vendor with requests cannot be deleted out from under them.
            entity.HasOne(p => p.Department)
                  .WithMany(d => d.PurchaseRequests)
                  .HasForeignKey(p => p.DepartmentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Vendor)
                  .WithMany(v => v.PurchaseRequests)
                  .HasForeignKey(p => p.VendorId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Supports dashboard counts and department/date filtering.
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => new { p.DepartmentId, p.SubmittedDate });

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_PurchaseRequests_Amount_Positive", "[Amount] > 0");
                t.HasCheckConstraint("CK_PurchaseRequests_Status",
                    "[Status] IN ('Pending', 'Approved', 'Rejected')");
            });
        });

        modelBuilder.Entity<ApprovalRecord>(entity =>
        {
            entity.Property(a => a.Action).HasConversion<string>().HasMaxLength(20);

            // Restrict: audit history must never be cascade-deleted.
            entity.HasOne(a => a.PurchaseRequest)
                  .WithMany(p => p.ApprovalRecords)
                  .HasForeignKey(a => a.PurchaseRequestId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ApprovalRecords_Action",
                    "[Action] IN ('Approved', 'Rejected')");

                // The explicit IS NOT NULL matters: a CHECK constraint that evaluates to
                // NULL (unknown) PASSES in SQL Server, so LEN(TRIM(NULL)) > 0 alone would
                // let a rejection with NULL notes through.
                t.HasCheckConstraint("CK_ApprovalRecords_RejectionRequiresNotes",
                    "[Action] <> 'Rejected' OR ([Notes] IS NOT NULL AND LEN(TRIM([Notes])) > 0)");
            });
        });
    }
}