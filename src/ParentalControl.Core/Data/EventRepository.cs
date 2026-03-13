using System.Globalization;
using Microsoft.Data.Sqlite;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

public static class EventRepository
{
    public static void LogEvent(string userSid, EventType eventType, string details = "")
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO events (timestamp, user_sid, event_type, details)
            VALUES (@timestamp, @userSid, @eventType, @details)
            """;
        cmd.Parameters.AddWithValue("@timestamp", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
        cmd.Parameters.AddWithValue("@userSid", userSid);
        cmd.Parameters.AddWithValue("@eventType", eventType.ToString());
        cmd.Parameters.AddWithValue("@details", details);
        cmd.ExecuteNonQuery();
    }

    public static List<EventRecord> GetEvents(DateTime? from = null, DateTime? to = null, string? userSid = null)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();

        var conditions = new List<string>();
        if (from.HasValue)
        {
            conditions.Add("timestamp >= @from");
            cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
        }
        if (to.HasValue)
        {
            conditions.Add("timestamp <= @to");
            cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
        }
        if (!string.IsNullOrEmpty(userSid))
        {
            conditions.Add("user_sid = @userSid");
            cmd.Parameters.AddWithValue("@userSid", userSid);
        }

        var whereClause = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : "";

        cmd.CommandText = $"SELECT timestamp, user_sid, event_type, details FROM events {whereClause} ORDER BY timestamp DESC LIMIT 10000";

        var events = new List<EventRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!Enum.TryParse<EventType>(reader.GetString(2), out var eventType))
                continue;

            events.Add(new EventRecord
            {
                Timestamp = DateTime.ParseExact(reader.GetString(0), "yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
                UserSid = reader.GetString(1),
                EventType = eventType,
                Details = reader.GetString(3)
            });
        }
        return events;
    }

    public static int DeleteOlderThan(DateTime cutoff)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM events WHERE timestamp < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
        return cmd.ExecuteNonQuery();
    }
}
