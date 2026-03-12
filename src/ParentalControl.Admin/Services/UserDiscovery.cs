using System.DirectoryServices.AccountManagement;

namespace ParentalControl.Admin.Services;

public static class UserDiscovery
{
    public static List<(string Sid, string Username)> GetNonAdminLocalUsers()
    {
        return GetAllLocalUsers()
            .Where(u => !u.IsAdmin)
            .Select(u => (u.Sid, u.Username))
            .ToList();
    }

    public static List<(string Sid, string Username, bool IsAdmin)> GetAllLocalUsers()
    {
        var results = new List<(string, string, bool)>();

        using var context = new PrincipalContext(ContextType.Machine);
        using var searcher = new PrincipalSearcher(new UserPrincipal(context));
        using var adminGroup = GroupPrincipal.FindByIdentity(context, "Administrators");

        var adminMembers = adminGroup?.GetMembers().ToList() ?? [];

        foreach (var principal in searcher.FindAll())
        {
            if (principal is not UserPrincipal user) continue;
            if (user.Sid is null) continue;
            if (user.Enabled == false) continue;

            var username = user.SamAccountName;
            if (string.IsNullOrEmpty(username)) continue;

            // Skip well-known built-in accounts
            if (username.Equals("DefaultAccount", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("WDAGUtilityAccount", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("Guest", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isAdmin = adminMembers.Any(m => m.Sid?.Value == user.Sid.Value);
            results.Add((user.Sid.Value, username, isAdmin));
        }

        return results;
    }
}
