using Microsoft.Data.Sqlite;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

public static class UserRepository
{
    public static List<User> GetAll()
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, sid, username, is_restricted FROM users ORDER BY username";

        var users = new List<User>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            users.Add(ReadUser(reader));
        }
        return users;
    }

    public static User? GetBySid(string sid)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, sid, username, is_restricted FROM users WHERE sid = @sid";
        cmd.Parameters.AddWithValue("@sid", sid);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public static User Upsert(string sid, string username, bool isRestricted)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (sid, username, is_restricted)
            VALUES (@sid, @username, @restricted)
            ON CONFLICT(sid) DO UPDATE SET
                username = excluded.username,
                is_restricted = excluded.is_restricted
            RETURNING id, sid, username, is_restricted
            """;
        cmd.Parameters.AddWithValue("@sid", sid);
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@restricted", isRestricted ? 1 : 0);

        using var reader = cmd.ExecuteReader();
        reader.Read();
        return ReadUser(reader);
    }

    public static void SetRestricted(int userId, bool isRestricted)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET is_restricted = @restricted WHERE id = @id";
        cmd.Parameters.AddWithValue("@restricted", isRestricted ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.ExecuteNonQuery();
    }

    private static User ReadUser(SqliteDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt32(0),
            Sid = reader.GetString(1),
            Username = reader.GetString(2),
            IsRestricted = reader.GetInt32(3) != 0
        };
    }
}
