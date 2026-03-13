using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Data.Sqlite;

namespace ParentalControl.Core.Data;

public static class DatabaseManager
{
    private const string DataDirectory = @"C:\ProgramData\ParentalControl";
    private const string DatabaseFileName = "data.db";
    public const int RetentionDays = 30;

    public static string DatabasePath => Path.Combine(DataDirectory, DatabaseFileName);

    public static string ConnectionString => $"Data Source={DatabasePath}";

    public static void Initialize()
    {
        var dirInfo = new DirectoryInfo(DataDirectory);
        if (!dirInfo.Exists)
        {
            dirInfo.Create();
            var security = dirInfo.GetAccessControl();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            dirInfo.SetAccessControl(security);
        }

        using var connection = CreateConnection();

        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
        pragmaCmd.ExecuteNonQuery();

        using var schemaCmd = connection.CreateCommand();
        schemaCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sid TEXT NOT NULL UNIQUE,
                username TEXT NOT NULL,
                is_restricted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS limits (
                user_id INTEGER PRIMARY KEY,
                daily_minutes INTEGER NOT NULL DEFAULT 120,
                schedule_start TEXT NOT NULL DEFAULT '08:00',
                schedule_end TEXT NOT NULL DEFAULT '22:00',
                FOREIGN KEY (user_id) REFERENCES users(id)
            );

            CREATE TABLE IF NOT EXISTS usage (
                user_id INTEGER NOT NULL,
                date TEXT NOT NULL,
                minutes_used INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (user_id, date),
                FOREIGN KEY (user_id) REFERENCES users(id)
            );

            CREATE TABLE IF NOT EXISTS events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                user_sid TEXT NOT NULL,
                event_type TEXT NOT NULL,
                details TEXT NOT NULL DEFAULT ''
            );

            CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events (timestamp);
            CREATE INDEX IF NOT EXISTS idx_events_user_timestamp ON events (user_sid, timestamp);
            """;
        schemaCmd.ExecuteNonQuery();
    }

    public static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA busy_timeout=5000;";
        pragmaCmd.ExecuteNonQuery();

        return connection;
    }
}
