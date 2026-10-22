using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcureTrack.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementReportProcedure : Migration
    {
               protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Readable copy: reports/sql/usp_ProcurementReport.sql (keep the two in sync).
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_ProcurementReport
                    @StartDate    date,
                    @EndDate      date,
                    @DepartmentId int = NULL    -- NULL means all departments
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @StartDate IS NULL OR @EndDate IS NULL OR @EndDate < @StartDate
                        THROW 50001, 'Provide a valid date range: EndDate must be on or after StartDate.', 1;

                    SELECT
                        pr.Id                                  AS RequestId,
                        CONCAT('PR-', FORMAT(pr.Id, 'D5'))     AS RequestNumber,
                        pr.Title,
                        d.Name                                 AS Department,
                        v.Name                                 AS Vendor,
                        pr.Amount,
                        pr.Status,
                        CAST(pr.SubmittedDate AS date)         AS SubmittedDate,
                        pr.RequiredByDate,
                        pr.RequestedBy,
                        lastDecision.ActedBy                   AS DecidedBy,
                        CAST(lastDecision.ActedAt AS date)     AS DecisionDate
                    FROM dbo.PurchaseRequests AS pr
                    INNER JOIN dbo.Departments AS d ON d.Id = pr.DepartmentId
                    INNER JOIN dbo.Vendors     AS v ON v.Id = pr.VendorId
                    OUTER APPLY (
                        SELECT TOP (1) a.ActedBy, a.ActedAt
                        FROM dbo.ApprovalRecords AS a
                        WHERE a.PurchaseRequestId = pr.Id
                        ORDER BY a.ActedAt DESC
                    ) AS lastDecision
                    WHERE pr.SubmittedDate >= @StartDate
                      AND pr.SubmittedDate <  DATEADD(day, 1, @EndDate)
                      AND (@DepartmentId IS NULL OR pr.DepartmentId = @DepartmentId)
                    ORDER BY d.Name, pr.Status, pr.SubmittedDate
                    OPTION (RECOMPILE);  -- builds a fresh plan for "all" vs. "one department"
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ProcurementReport;");
        }
    }
}
