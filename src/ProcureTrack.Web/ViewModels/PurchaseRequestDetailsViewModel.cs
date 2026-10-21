using ProcureTrack.Web.Models;

namespace ProcureTrack.Web.ViewModels;

public class PurchaseRequestDetailsViewModel
{
    public required PurchaseRequest Request { get; init; }

    /// <summary>Pending, owned by the current user, and the user is a Requester.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Pending, the user is an Approver, and the request is not their own.</summary>
    public bool CanDecide { get; init; }

    public bool IsOwnRequest { get; init; }

    public DecisionViewModel Decision { get; init; } = new();
}