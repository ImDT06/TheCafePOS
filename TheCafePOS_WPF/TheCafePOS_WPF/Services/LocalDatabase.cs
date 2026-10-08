using Microsoft.Data.Sqlite;
using System.IO;
using System.Text.Json;

namespace TheCafePOS_WPF.Services;

// Each document is replaced atomically. Checkout stores orders, stock and shifts
// together so a failed write cannot leave a partially completed sale.
public sealed class LocalDatabase
{
    static LocalDatabase() { }
    public static LocalDatabase Instance { get; } = new();
    public string DatabasePath { get; }
    public LocalDatabase(string? path = null)
    {
        var directory = Environment.GetEnvironmentVariable("THECAFEPOS_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheCafePOS");
        DatabasePath = path ?? Path.Combine(directory, "cafe.db");
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        SQLitePCL.Batteries_V2.Init();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS documents (id TEXT PRIMARY KEY, payload TEXT NOT NULL); PRAGMA user_version=1;";
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        connection.Open();
        return connection;
    }
    public T? Read<T>(string key)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM documents WHERE id=$id";
        command.Parameters.AddWithValue("$id", key);
        var json = command.ExecuteScalar() as string;
        return json is null ? default : JsonSerializer.Deserialize<T>(json) ?? throw new InvalidDataException("Dữ liệu lưu trữ không hợp lệ.");
    }
    public void Write<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO documents(id,payload) VALUES($id,$json) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload";
        command.Parameters.AddWithValue("$id", key);
        command.Parameters.AddWithValue("$json", json);
        command.ExecuteNonQuery();
        transaction.Commit();
    }
    public void WritePair<T, U>(string firstKey, T first, string secondKey, U second)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        foreach (var pair in new[] { (firstKey, JsonSerializer.Serialize(first)), (secondKey, JsonSerializer.Serialize(second)) })
        {
            using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO documents(id,payload) VALUES($id,$json) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload";
            command.Parameters.AddWithValue("$id", pair.Item1); command.Parameters.AddWithValue("$json", pair.Item2); command.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
