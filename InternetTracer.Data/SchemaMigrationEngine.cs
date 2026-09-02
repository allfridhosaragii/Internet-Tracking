namespace InternetTracer.Data;

using Dapper;

public class SchemaMigrationEngine
{
    private readonly DatabaseFactory _dbFactory;

    public SchemaMigrationEngine(DatabaseFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public void Migrate()
    {
        using var connection = _dbFactory.CreateConnection();
        using var transaction = connection.BeginTransaction();

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS interfaces (
                id TEXT PRIMARY KEY,
                system_guid TEXT,
                name TEXT,
                type TEXT,
                description TEXT,
                first_seen_utc TEXT,
                last_seen_utc TEXT
            );

            CREATE TABLE IF NOT EXISTS networks (
                id TEXT PRIMARY KEY,
                fingerprint_hash TEXT,
                display_name TEXT,
                ssid TEXT,
                bssid TEXT,
                gateway TEXT,
                subnet TEXT,
                connection_type TEXT,
                first_seen_utc TEXT,
                last_seen_utc TEXT
            );

            CREATE TABLE IF NOT EXISTS applications (
                id TEXT PRIMARY KEY,
                executable_path TEXT,
                display_name TEXT,
                publisher TEXT,
                first_seen_utc TEXT,
                last_seen_utc TEXT
            );

            CREATE TABLE IF NOT EXISTS traffic_minute (
                bucket_utc TEXT NOT NULL,
                interface_id TEXT NOT NULL,
                network_id TEXT,
                application_id TEXT,
                download_bytes INTEGER NOT NULL,
                upload_bytes INTEGER NOT NULL,
                sample_count INTEGER NOT NULL,
                attribution_state INTEGER NOT NULL,
                PRIMARY KEY (bucket_utc, interface_id, application_id)
            );

            CREATE INDEX IF NOT EXISTS idx_traffic_minute_bucket ON traffic_minute(bucket_utc);
        ", transaction: transaction);

        transaction.Commit();
    }
}
