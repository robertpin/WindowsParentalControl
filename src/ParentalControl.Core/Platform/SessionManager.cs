using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ParentalControl.Core.Platform;

public static class SessionManager
{
    public static bool ForceLogoff(int sessionId)
    {
        return NativeMethods.WTSLogoffSession(
            NativeMethods.WTS_CURRENT_SERVER_HANDLE,
            sessionId,
            false);
    }

    public static string? GetSessionUsername(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformationW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                NativeMethods.WTS_INFO_CLASS.WTSUserName,
                out var buffer,
                out _))
        {
            return null;
        }

        try
        {
            var username = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrEmpty(username) ? null : username;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    public static string? GetSessionDomain(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformationW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                NativeMethods.WTS_INFO_CLASS.WTSDomainName,
                out var buffer,
                out _))
        {
            return null;
        }

        try
        {
            var domain = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrEmpty(domain) ? null : domain;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    public static string? GetSessionUserSid(int sessionId)
    {
        var username = GetSessionUsername(sessionId);
        if (username is null) return null;

        var domain = GetSessionDomain(sessionId);
        var fullName = string.IsNullOrEmpty(domain) ? username : $"{domain}\\{username}";

        try
        {
            var account = new NTAccount(fullName);
            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));
            return sid.Value;
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }

    public static List<(int SessionId, string Username, string? Sid)> GetActiveSessions()
    {
        var sessions = new List<(int, string, string?)>();

        if (!NativeMethods.WTSEnumerateSessionsW(
                NativeMethods.WTS_CURRENT_SERVER_HANDLE,
                0,
                1,
                out var pSessionInfo,
                out var count))
        {
            return sessions;
        }

        try
        {
            var structSize = Marshal.SizeOf<NativeMethods.WTS_SESSION_INFO>();
            for (int i = 0; i < count; i++)
            {
                var current = Marshal.PtrToStructure<NativeMethods.WTS_SESSION_INFO>(
                    pSessionInfo + i * structSize);

                if (current.State != NativeMethods.WTS_CONNECTSTATE_CLASS.WTSActive)
                    continue;

                var username = GetSessionUsername(current.SessionID);
                if (string.IsNullOrEmpty(username))
                    continue;

                var sid = GetSessionUserSid(current.SessionID);
                sessions.Add((current.SessionID, username, sid));
            }
        }
        finally
        {
            NativeMethods.WTSFreeMemory(pSessionInfo);
        }

        return sessions;
    }
}
