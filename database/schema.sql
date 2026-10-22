IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Departments] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(10) NOT NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
);

CREATE TABLE [Vendors] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    [ContactEmail] nvarchar(256) NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Vendors] PRIMARY KEY ([Id])
);

CREATE TABLE [PurchaseRequests] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(250) NOT NULL,
    [Description] nvarchar(2000) NULL,
    [DepartmentId] int NOT NULL,
    [VendorId] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [BusinessJustification] nvarchar(2000) NOT NULL,
    [RequestedBy] nvarchar(100) NOT NULL,
    [SubmittedDate] datetime2 NOT NULL,
    [RequiredByDate] date NOT NULL,
    [Status] nvarchar(20) NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_PurchaseRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PurchaseRequests_Amount_Positive] CHECK ([Amount] > 0),
    CONSTRAINT [CK_PurchaseRequests_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected')),
    CONSTRAINT [FK_PurchaseRequests_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PurchaseRequests_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [ApprovalRecords] (
    [Id] int NOT NULL IDENTITY,
    [PurchaseRequestId] int NOT NULL,
    [Action] nvarchar(20) NOT NULL,
    [Notes] nvarchar(1000) NULL,
    [ActedBy] nvarchar(100) NOT NULL,
    [ActedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ApprovalRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ApprovalRecords_Action] CHECK ([Action] IN ('Approved', 'Rejected')),
    CONSTRAINT [CK_ApprovalRecords_RejectionRequiresNotes] CHECK ([Action] <> 'Rejected' OR ([Notes] IS NOT NULL AND LEN(TRIM([Notes])) > 0)),
    CONSTRAINT [FK_ApprovalRecords_PurchaseRequests_PurchaseRequestId] FOREIGN KEY ([PurchaseRequestId]) REFERENCES [PurchaseRequests] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ApprovalRecords_PurchaseRequestId] ON [ApprovalRecords] ([PurchaseRequestId]);

CREATE UNIQUE INDEX [IX_Departments_Code] ON [Departments] ([Code]);

CREATE UNIQUE INDEX [IX_Departments_Name] ON [Departments] ([Name]);

CREATE INDEX [IX_PurchaseRequests_DepartmentId_SubmittedDate] ON [PurchaseRequests] ([DepartmentId], [SubmittedDate]);

CREATE INDEX [IX_PurchaseRequests_Status] ON [PurchaseRequests] ([Status]);

CREATE INDEX [IX_PurchaseRequests_VendorId] ON [PurchaseRequests] ([VendorId]);

CREATE UNIQUE INDEX [IX_Vendors_Name] ON [Vendors] ([Name]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261021171536_InitialSchema', N'10.0.13');

COMMIT;
GO

BEGIN TRANSACTION;
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

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261022153615_AddProcurementReportProcedure', N'10.0.13');

COMMIT;
GO

