using ProcureTrack.Web.Models;

namespace ProcureTrack.Web.ViewModels;

public class PurchaseRequestDetailsViewModel
{
    public required PurchaseRequest Request { get; init; }

    public bool CanEdit { get; init; }

    public bool CanDecide { get; init; }

    public DecisionViewModel Decision { get; init; } = new();
}