namespace InternetTracer.Data;

using Microsoft.Data.Sqlite;
using System.IO;

public class DatabaseFactory
{
    private readonly string _dbPath;

    public DatabaseFactory(string dbPath)
    {
        _dbPath = dbPath;
    }

    public SqliteConnection CreateConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        
        // Ensure WAL mode for concurrency
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        command.ExecuteNonQuery();

        return connection;
    }
}
