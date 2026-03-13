using Microsoft.Data.Sqlite;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

public static class UsageRepository
{
    public static UsageRecord? GetUsage(int userId, DateOnly date)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT user_id, date, minutes_used FROM usage WHERE user_id = @userId AND date = @date";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return ReadUsageRecord(reader);
    }

    public static void AddMinutes(int userId, DateOnly date, int minutes)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage (user_id, date, minutes_used)
            VALUES (@userId, @date, @minutes)
            ON CONFLICT(user_id, date) DO UPDATE SET
                minutes_used = minutes_used + @minutes
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@minutes", minutes);
        cmd.ExecuteNonQuery();
    }

    public static List<UsageRecord> GetUsageRange(int userId, DateOnly from, DateOnly to)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, date, minutes_used FROM usage
            WHERE user_id = @userId AND date >= @from AND date <= @to
            ORDER BY date
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd"));

        var records = new List<UsageRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadUsageRecord(reader));
        }
        return records;
    }

    public static int DeleteOlderThan(DateOnly cutoff)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM usage WHERE date < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("yyyy-MM-dd"));
        return cmd.ExecuteNonQuery();
    }

    private static UsageRecord ReadUsageRecord(SqliteDataReader reader)
    {
        return new UsageRecord
        {
            UserId = reader.GetInt32(0),
            Date = DateOnly.Parse(reader.GetString(1)),
            MinutesUsed = reader.GetInt32(2)
        };
    }
}
