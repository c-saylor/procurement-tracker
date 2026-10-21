namespace ProcureTrack.Web.Security;

/// <summary>Role names used in claims and [Authorize] attributes, defined once to avoid typos.</summary>
public static class AppRoles
{
    public const string Requester = "Requester";
    public const string Approver = "Approver";
}