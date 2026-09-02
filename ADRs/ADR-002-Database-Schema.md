# ADR-002: Database Schema & Retention

## Status
Proposed

## Context
Internet Tracer must store high-resolution network traffic samples and attribute them to processes and network identities without ballooning memory or disk usage indefinitely. The Master Specification forbids relying exclusively on large cloud-like data lakes, preferring local privacy-first storage.

## Decision
We will use **SQLite** (via `Microsoft.Data.Sqlite` and `Dapper`) as the exclusive local storage engine.
The database will operate in `WAL` (Write-Ahead Logging) mode with `NORMAL` synchronous mode to optimize for background appending.

### Schema Design
1. **interfaces**: Stores system network adapters (Wi-Fi, Ethernet). Identifies by GUID.
2. **networks**: Stores unique network identities (e.g., specific Wi-Fi networks). Identifies by a deterministic hash (SSID + BSSID + Gateway).
3. **traffic_minute**: The primary time-series table.
   - `bucket_utc`: Minute-aligned timestamp.
   - `interface_id`: Link to the adapter.
   - `network_id`: Link to the network identity.
   - `application_id`: Link to the process/application.
   - `download_bytes`, `upload_bytes`: Aggregated totals.
   - `sample_count`: Number of raw samples that contributed to this bucket.
   - `attribution_state`: Enum tracking confidence (Attributed, Unattributed, Partially).
   - Primary Key: `(bucket_utc, interface_id, network_id, application_id)` -> This enables UPSERT logic (`ON CONFLICT DO UPDATE SET...`).

### Retention Strategy (To Be Implemented)
- **1-Second Raw Samples**: NOT persisted to SQLite. Held in memory only for real-time IPC streaming to the UI.
- **Minute Aggregates**: Stored for 72 hours, then rolled up.
- **Hourly Aggregates**: Stored for 30 days.
- **Daily Aggregates**: Stored indefinitely, forming the historical record.

## Consequences
- **Positive**: We avoid the massive I/O overhead of writing 60 rows per minute per interface per application to disk. The `MinuteAggregator` absorbs the bursty traffic and writes exactly one UPSERT per combination per minute.
- **Negative**: If the background service crashes hard, we may lose up to 59 seconds of telemetry. This is acceptable for a desktop network monitor, provided the database itself never corrupts (prevented by WAL).
