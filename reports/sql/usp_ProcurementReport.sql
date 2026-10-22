/*
    ProcureTrack procurement report procedure
    -----------------------------------------
    Source of truth: EF migration "AddProcurementReportProcedure"
    (src/ProcureTrack.Web/Migrations). This copy exists so reviewers can read the
    report SQL without opening C#. Keep the two in sync.

    Used by: reports/ProcurementReport.rdl (dataset "ProcurementDetail")

    Parameters
      @StartDate     date  required  first submission date included
      @EndDate       date  required  last submission date included (whole day)
      @DepartmentId  int   optional  NULL = all departments

    Example
      EXEC dbo.usp_ProcurementReport
           @StartDate = '2026-09-01', @EndDate = '2026-10-31', @DepartmentId = NULL;

    Note: SubmittedDate is stored in UTC; date boundaries are UTC dates.
*/
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