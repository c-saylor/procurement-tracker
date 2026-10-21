using ProcureTrack.Web.Models;

namespace ProcureTrack.Web.Services;

/// <summary>
/// Enforces purchase request approval rules. Contains no database or HTTP code,
/// so every rule can be unit tested directly. Controllers must route all status changes through this class to ensure rules are enforced consistently.
/// </summary>
public class ApprovalWorkflow
{
    public const int MaxActorNameLength = 100;
    public const int MaxNotesLength = 1000;

    private readonly TimeProvider _timeProvider;

    public ApprovalWorkflow(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>Only pending requests may be changed through ordinary editing</summary>
    public bool CanEdit(PurchaseRequest request) => request.Status == RequestStatus.Pending;

    /// <summary>Only pending requests may be deleted. Decided requests are part of the audit trail</summary>
    public bool CanDelete(PurchaseRequest request) => request.Status == RequestStatus.Pending;

    ///<summary>Only pending requests may be approved or rejected</summary>
    public bool CanDecide(PurchaseRequest request) => request.Status == RequestStatus.Pending;

    public WorkflowResult Approve(PurchaseRequest request, string? approvedBy, string? notes) =>
        Decide(request, ApprovalAction.Approved, approvedBy, notes, notesRequired: false);

    public WorkflowResult Reject(PurchaseRequest request, string? rejectedBy, string? reason) =>
        Decide(request, ApprovalAction.Rejected, rejectedBy, reason, notesRequired: true);

    private WorkflowResult Decide(
        PurchaseRequest request,
        ApprovalAction action,
        string? actor,
        string? notes,
        bool notesRequired)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Every check runs before anything is modified, so a failure leaves the request unchanged.
        if (!CanDecide(request))
        {
            return WorkflowResult.Failure(
                $"Only pending requests can be approved or rejected. This request is {request.Status}.");
        }

        var actorName = actor?.Trim();
        if (string.IsNullOrEmpty(actorName))
        {
            return WorkflowResult.Failure("The approver's name is required.");
        }

        if (actorName.Length > MaxActorNameLength)
        {
            return WorkflowResult.Failure(
                $"The approver's name cannot exceed {MaxActorNameLength} characters.");
        }

        // Separation of duties: nobody decides their own request.
        if (string.Equals(actorName, request.RequestedBy.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowResult.Failure("Requesters cannot approve or reject their own requests.");
        }

        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (notesRequired && trimmedNotes is null)
        {
            return WorkflowResult.Failure("A reason is required to reject a request.");
        }

        if (trimmedNotes is not null && trimmedNotes.Length > MaxNotesLength)
        {
            return WorkflowResult.Failure(
                $"Notes cannot exceed {MaxNotesLength} characters.");
        }

        request.Status = action == ApprovalAction.Approved ? RequestStatus.Approved : RequestStatus.Rejected;
        request.ApprovalRecords.Add(new ApprovalRecord
        {
            Action = action,
            Notes = trimmedNotes,
            ActedBy = actorName,
            ActedAt = _timeProvider.GetUtcNow().UtcDateTime,
        });

        return WorkflowResult.Success();
    }
}