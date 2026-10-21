namespace ProcureTrack.Web.Models;

/// <summary>Current lifecycle state of a purchase request.</summary>
public enum RequestStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>Decision recorded in the approval audit trail.</summary>
public enum ApprovalAction
{
    Approved,
    Rejected
}