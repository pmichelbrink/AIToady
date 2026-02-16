using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

public class HubDatabase
{
    private readonly string _connectionString;

    public HubDatabase()
    {
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIToady", "harvesters.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";
        
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        new SqliteCommand(@"
            CREATE TABLE IF NOT EXISTS harvesters (id TEXT PRIMARY KEY, status TEXT);
            CREATE TABLE IF NOT EXISTS logs (id INTEGER PRIMARY KEY AUTOINCREMENT, harvester_id TEXT, message TEXT, timestamp DATETIME DEFAULT CURRENT_TIMESTAMP);
        ", conn).ExecuteNonQuery();
    }

    public void UpdateStatus(string id, string status)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = new SqliteCommand("INSERT OR REPLACE INTO harvesters (id, status) VALUES (@id, @status)", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@status", status);
        cmd.ExecuteNonQuery();
    }

    public void AddLog(string id, string message)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var cmd = new SqliteCommand("INSERT INTO logs (harvester_id, message) VALUES (@id, @msg)", conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@msg", message);
        cmd.ExecuteNonQuery();
    }

    public Dictionary<string, HarvesterData> GetAll()
    {
        var result = new Dictionary<string, HarvesterData>();
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        
        var cmd = new SqliteCommand("SELECT id, status FROM harvesters", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            result[id] = new HarvesterData { Status = reader.GetString(1), Logs = new List<LogEntry>() };
        }

        foreach (var id in result.Keys)
        {
            var logCmd = new SqliteCommand("SELECT message, timestamp FROM logs WHERE harvester_id = @id ORDER BY timestamp DESC LIMIT 100", conn);
            logCmd.Parameters.AddWithValue("@id", id);
            using var logReader = logCmd.ExecuteReader();
            while (logReader.Read())
                result[id].Logs.Add(new LogEntry { Message = logReader.GetString(0), Timestamp = logReader.GetString(1) });
        }

        return result;
    }

    public void Delete(string id)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        new SqliteCommand("DELETE FROM harvesters WHERE id = @id", conn) { Parameters = { new("@id", id) } }.ExecuteNonQuery();
        new SqliteCommand("DELETE FROM logs WHERE harvester_id = @id", conn) { Parameters = { new("@id", id) } }.ExecuteNonQuery();
    }
}

public class HarvesterData
{
    public string Status { get; set; }
    public List<LogEntry> Logs { get; set; }
}

public class LogEntry
{
    public string Message { get; set; }
    public string Timestamp { get; set; }
    public string Log { get; set; }
}
