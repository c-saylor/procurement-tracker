using ProcureTrack.Web.Models;
using ProcureTrack.Web.Services;

namespace ProcureTrack.Tests;

public class ApprovalWorkflowTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 22, 15, 30, 0, TimeSpan.Zero);

    private readonly ApprovalWorkflow _workflow = new(new FixedTimeProvider(FixedNow));

    [Fact]
    public void Approve_PendingRequest_SetsApprovedAndRecordsAudit()
    {
        var request = CreateRequest();

        var result = _workflow.Approve(request, "Morgan Alvarez", "   Priority approval.   ");

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.Equal(RequestStatus.Approved, request.Status);

        var record = Assert.Single(request.ApprovalRecords);
        Assert.Equal(ApprovalAction.Approved, record.Action);
        Assert.Equal("Morgan Alvarez", record.ActedBy);
        Assert.Equal("Priority approval.", record.Notes);
        Assert.Equal(FixedNow.UtcDateTime, record.ActedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Approve_WithBlankNotes_SucceedsAndStoresNullNotes(string? notes)
    {
        var request = CreateRequest();

        var result = _workflow.Approve(request, "Morgan Alvarez", notes);

        Assert.True(result.Succeeded);
        Assert.Null(Assert.Single(request.ApprovalRecords).Notes);
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    public void Approve_AlreadyDecidedRequest_Fails(RequestStatus status)
    {
        var request = CreateRequest(status);

        var result = _workflow.Approve(request, "Morgan Alvarez", null);

        AssertFailedWithoutChanges(result, request, status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Approve_WithoutApproverName_Fails(string? approver)
    {
        var request = CreateRequest();

        var result = _workflow.Approve(request, approver, null);

        AssertFailedWithoutChanges(result, request, RequestStatus.Pending);
    }

    [Theory]
    [InlineData("Hannah Cho")]
    [InlineData("  hannah cho  ")]
    public void Approve_ByRequester_FailsForSeparationOfDuties(string approver)
    {
        var request = CreateRequest();

        var result = _workflow.Approve(request, approver, null);

        AssertFailedWithoutChanges(result, request, RequestStatus.Pending);
    }

    [Fact]
    public void Approve_NotesOverMaximumLength_Fails()
    {
        var request = CreateRequest();
        var tooLong = new string('x', ApprovalWorkflow.MaxNotesLength + 1);

        var result = _workflow.Approve(request, "Morgan Alvarez", tooLong);

        AssertFailedWithoutChanges(result, request, RequestStatus.Pending);
    }

    // ---------- Reject ----------

    [Fact]
    public void Reject_PendingRequestWithReason_SetsRejectedAndStoresTrimmedReason()
    {
        var request = CreateRequest();

        var result = _workflow.Reject(request, "Morgan Alvarez", "  Duplicate of an existing order.  ");

        Assert.True(result.Succeeded);
        Assert.Equal(RequestStatus.Rejected, request.Status);

        var record = Assert.Single(request.ApprovalRecords);
        Assert.Equal(ApprovalAction.Rejected, record.Action);
        Assert.Equal("Duplicate of an existing order.", record.Notes);
        Assert.Equal(FixedNow.UtcDateTime, record.ActedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithoutReason_Fails(string? reason)
    {
        var request = CreateRequest();

        var result = _workflow.Reject(request, "Morgan Alvarez", reason);

        AssertFailedWithoutChanges(result, request, RequestStatus.Pending);
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    public void Reject_AlreadyDecidedRequest_Fails(RequestStatus status)
    {
        var request = CreateRequest(status);

        var result = _workflow.Reject(request, "Morgan Alvarez", "Changed my mind.");

        AssertFailedWithoutChanges(result, request, status);
    }

    // ---------- Edit / delete permissions ----------

    [Theory]
    [InlineData(RequestStatus.Pending, true)]
    [InlineData(RequestStatus.Approved, false)]
    [InlineData(RequestStatus.Rejected, false)]
    public void CanEditAndCanDelete_OnlyAllowPendingRequests(RequestStatus status, bool expected)
    {
        var request = CreateRequest(status);

        Assert.Equal(expected, _workflow.CanEdit(request));
        Assert.Equal(expected, _workflow.CanDelete(request));
    }

    // ---------- Helpers ----------

    private static PurchaseRequest CreateRequest(RequestStatus status = RequestStatus.Pending) => new()
    {
        Title = "Network switch replacement",
        Amount = 5830.00m,
        BusinessJustification = "Failing core switch is causing outages.",
        RequestedBy = "Hannah Cho",
        SubmittedDate = FixedNow.UtcDateTime.AddDays(-3),
        RequiredByDate = new DateOnly(2026, 11, 15),
        Status = status,
    };

    private static void AssertFailedWithoutChanges(
        WorkflowResult result, PurchaseRequest request, RequestStatus expectedStatus)
    {
        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(expectedStatus, request.Status);
        Assert.Empty(request.ApprovalRecords);
    }

    /// <summary>A clock frozen at a known instant, so timestamp assertions are exact.</summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}