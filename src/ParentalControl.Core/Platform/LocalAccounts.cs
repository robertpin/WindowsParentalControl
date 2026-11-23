using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace ParentalControl.Core.Platform;

/// <summary>
/// Local account helpers that work on any Windows display language: groups and built-in
/// accounts are identified by SID / RID instead of their (localized) names.
/// </summary>
public static class LocalAccounts
{
    private static readonly string AdministratorsSid =
        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value;

    // Guest (501), DefaultAccount (503), WDAGUtilityAccount (504)
    private static readonly string[] BuiltInAccountRids = ["-501", "-503", "-504"];

    /// <summary>
    /// SIDs of all members of the local Administrators group, including members of nested groups.
    /// </summary>
    public static HashSet<string> GetAdministratorSids()
    {
        var sids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var context = new PrincipalContext(ContextType.Machine);
        using var adminGroup = GroupPrincipal.FindByIdentity(context, IdentityType.Sid, AdministratorsSid);
        if (adminGroup is null) return sids;

        foreach (var member in adminGroup.GetMembers(recursive: true))
        {
            using (member)
            {
                if (member.Sid is not null)
                    sids.Add(member.Sid.Value);
            }
        }

        return sids;
    }

    /// <summary>
    /// True if the user is a member of the local Administrators group.
    /// Returns false if membership cannot be determined, so restrictions stay in force.
    /// </summary>
    public static bool IsAdministrator(string userSid)
    {
        try
        {
            return GetAdministratorSids().Contains(userSid);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsBuiltInAccount(string userSid) =>
        BuiltInAccountRids.Any(rid => userSid.EndsWith(rid, StringComparison.Ordinal));
}
