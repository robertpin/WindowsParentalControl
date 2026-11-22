using System.DirectoryServices.AccountManagement;
using ParentalControl.Core.Platform;

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

        // Looked up by SID: the group name is localized ("Administrators", "Администраторы", ...)
        var adminSids = LocalAccounts.GetAdministratorSids();

        foreach (var principal in searcher.FindAll())
        {
            if (principal is not UserPrincipal user) continue;
            if (user.Sid is null) continue;
            if (user.Enabled == false) continue;

            var username = user.SamAccountName;
            if (string.IsNullOrEmpty(username)) continue;

            // Skip well-known built-in accounts (by RID, their names are localized too)
            if (LocalAccounts.IsBuiltInAccount(user.Sid.Value)) continue;

            var isAdmin = adminSids.Contains(user.Sid.Value);
            results.Add((user.Sid.Value, username, isAdmin));
        }

        return results;
    }
}
