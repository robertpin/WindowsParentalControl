using Microsoft.Data.Sqlite;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

public static class LimitRepository
{
    public static LimitConfig? GetByUserId(int userId)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT user_id, daily_minutes, schedule_start, schedule_end FROM limits WHERE user_id = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new LimitConfig
        {
            UserId = reader.GetInt32(0),
            DailyMinutes = reader.GetInt32(1),
            ScheduleStart = TimeOnly.Parse(reader.GetString(2)),
            ScheduleEnd = TimeOnly.Parse(reader.GetString(3))
        };
    }

    public static void Upsert(LimitConfig config)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO limits (user_id, daily_minutes, schedule_start, schedule_end)
            VALUES (@userId, @dailyMinutes, @start, @end)
            ON CONFLICT(user_id) DO UPDATE SET
                daily_minutes = excluded.daily_minutes,
                schedule_start = excluded.schedule_start,
                schedule_end = excluded.schedule_end
            """;
        cmd.Parameters.AddWithValue("@userId", config.UserId);
        cmd.Parameters.AddWithValue("@dailyMinutes", config.DailyMinutes);
        cmd.Parameters.AddWithValue("@start", config.ScheduleStart.ToString("HH:mm"));
        cmd.Parameters.AddWithValue("@end", config.ScheduleEnd.ToString("HH:mm"));
        cmd.ExecuteNonQuery();
    }
}
