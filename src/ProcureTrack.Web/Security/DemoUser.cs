namespace ProcureTrack.Web.Security;

public sealed record DemoUser(string Key, string DisplayName, string Title, string[] Roles);

/// <summary>
/// Fictional demo identities for showcasing role-based authorization.
/// This is an identity switcher, not production authentication: there are no passwords.
/// </summary>
public static class DemoUsers
{
    public static IReadOnlyList<DemoUser> All { get; } =
    [
        new("dana", "Dana Whitfield", "Facilities Coordinator", [AppRoles.Requester]),
        new("morgan", "Morgan Alvarez", "Procurement Manager", [AppRoles.Approver]),
        new("priya", "Priya Natarajan", "Project Controls Lead", [AppRoles.Requester, AppRoles.Approver]),
    ];

    public static DemoUser? Find(string? key) => All.FirstOrDefault(u => u.Key == key);
}