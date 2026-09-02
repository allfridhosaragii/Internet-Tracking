# K16 Traffic Explorer Architecture Reconnaissance Report

**Date:** 2026-09-02  
**Author:** Principal Engineer (Qoder)  
**Purpose:** Evaluate readiness for K16 — Traffic Explorer feature implementation  

---

## Executive Summary

Internet Tracer has a **solid foundational architecture** built over phases K1-K15, with working core telemetry collection, SQLite persistence, IPC communication, and a functional Dashboard. The codebase is at **.NET 9.0**, uses **WinUI 3** for UI, and follows a strict service/UI separation via Named Pipes.

The existing system can support Traffic Explorer, but several enhancements are required:

1. **Historical query infrastructure** already exists but needs extension for Traffic Explorer filters
2. **Database schema** supports minute-level aggregates but lacks higher-level aggregations (hourly/daily) defined in ADR-002
3. **Time resolution strategy** must be explicitly documented and enforced
4. **Indexing** exists for bucket_utc but lacks compound indexes for efficient filtering by network/application
5. **Dashboard is LOCKED** - Traffic Explorer must not introduce regressions

---

## 1. Repository Architecture

### 1.1 Project Structure

```
InternetTracer.sln contains 10 projects:

Core Libraries:
├─ InternetTracer.Core      # Domain models, contracts, business logic
├─ InternetTracer.Data      # SQLite persistence, aggregation, queries
├─ InternetTracer.Monitor   # Network interface monitoring, ETW tracing
├─ InternetTracer.Ipc       # Named Pipe server/client protocol
└─ InternetTracer.Analytics # (Not yet implemented)

Services/Infrastructure:
├─ InternetTracer.Service   # Windows Service, background collector worker
├─ InternetTracer.App       # WinUI 3 desktop application shell + pages
└─ InternetTracker.SystemTests # Integration/system tests

Test Projects:
├─ InternetTracer.Tests     # Unit tests
└─ InternetTracer.Monitor.ConsoleTester # ETW prototype (legacy)
└─ InternetTracer.Monitor.EtwPrototype # ETW testing (legacy)
```

### 1.2 Dependencies & Technology Stack

| Component | Version | Status |
|-----------|---------|--------|
| .NET Target Framework | `net9.0` | ✅ Accepted (ADR-001) |
| WinUI 3 | Latest SDK | ✅ Used for UI |
| SQLite | Microsoft.Data.Sqlite | ✅ Used with Dapper |
| IPC Protocol | Named Pipes | ✅ Implemented |
| Process Attribution | ETW Kernel Provider | ✅ Implemented (ADR-006) |
| Network Identity | Hash-based fingerprinting | ✅ Implemented (ADR-007) |

### 1.3 Build State

```
BUILD: PASS (2 warnings, 0 errors)
Tests: PASS (8/8 tests passing)
Git Status: Clean (last commit: 023d2c1 - "feat: K15.3 live app traffic accumulation")
```

---

## 2. Current Data Flow

### 2.1 Monitoring Pipeline

```
Windows Network Interfaces
    ↓
WindowsNetworkInterfaceMonitor.GetInterfaces()
    ↓
TrafficDeltaCalculator.CalculateDeltas() → Handles counter resets
    ↓
Raw TrafficSamples[] (per-interface deltas)
```

### 2.2 Process Attribution (ETW)

```
ETW KernelTraceSession.NetworkTCPIP keywords
    ↓
TcpIpRecv/Send + UdpIpRecv/Send events
    ↓
(pid, rx_bytes, tx_bytes) tuples accumulated per second
    ↓
Proportional attribution: Interface_total × (pid_traffic / etw_total)
    ↓
If attribution ratio < 100% → remainder tracked as Unattributed
```

### 2.3 Storage Pipeline

```
Aggregated Samples [in-memory]
    ↓
MinuteAggregator.AddSample(TrafficSample)
    ↓
FlushOlderThanAsync(currentUtc - 2min) → Batch writes
    ↓
SQLite UPSERT INTO traffic_minute ON CONFLICT DO UPDATE
    ↓
WAL mode enabled for concurrency
```

### 2.4 Telemetry Query Path

```
UI Page → DashboardViewModel
    ↓
ITelemetryServiceApi (IPC Client)
    ↓
Named Pipe Request/Response (JSON-over-text-lines)
    ↓
IpcServer.ProcessRequestAsync()
    ↓
SqliteTelemetryQueryService.[MethodName]Async()
    ↓
Dapper → SQLite SELECT with parameterized queries
    ↓
Contract objects returned → JSON serialized back to UI
```

---

## 3. Current Database Schema

### 3.1 Tables (from SchemaMigrationEngine.cs)

#### `interfaces`
| Column | Type | Description |
|--------|------|-------------|
| id | TEXT PK | System GUID identifier |
| system_guid | TEXT | Windows adapter GUID |
| name | TEXT | Human-readable name |
| type | TEXT | NetworkInterfaceType enum string |
| description | TEXT | Adapter description |
| first_seen_utc | TEXT | ISO-8601 UTC timestamp |
| last_seen_utc | TEXT | ISO-8601 UTC timestamp |

#### `networks`
| Column | Type | Description |
|--------|------|-------------|
| id | TEXT PK | Fingerprint hash (SHA256 substring) |
| fingerprint_hash | TEXT | Full hash used for identity verification |
| display_name | TEXT | User-configurable alias |
| ssid | TEXT | Wi-Fi SSID (nullable) |
| bssid | TEXT | Access point MAC (nullable) |
| gateway | TEXT | Router IP address (nullable) |
| subnet | TEXT | Network prefix (nullable) |
| connection_type | TEXT | Wired/Wi-Fi/etc |
| first_seen_utc | TEXT | First observed |
| last_seen_utc | TEXT | Last updated |

#### `applications`
| Column | Type | Description |
|--------|------|-------------|
| id | TEXT PK | Executable path (stable key) |
| executable_path | TEXT | Full process path |
| display_name | TEXT | Normalized name |
| publisher | TEXT | Certificate publisher (nullable) |
| first_seen_utc | TEXT | First observed |
| last_seen_utc | TEXT | Last seen |

#### `traffic_minute` ← PRIMARY TIME-SERIES TABLE
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| bucket_utc | TEXT | NOT NULL, PK[0] | Minute-aligned UTC bucket |
| interface_id | TEXT | NOT NULL, PK[1] | Interface GUID |
| network_id | TEXT | PK[2], nullable | Network fingerprint hash |
| application_id | TEXT | PK[3], nullable | Executable path or NULL |
| download_bytes | INTEGER | NOT NULL | Cumulative RX bytes |
| upload_bytes | INTEGER | NOT NULL | Cumulative TX bytes |
| sample_count | INTEGER | NOT NULL | Number of raw samples aggregated |
| attribution_state | INTEGER | NOT NULL | 0=Attributed, 1=Partial, 2=Unattributed, 3=Failed |

**Primary Key:** `(bucket_utc, interface_id, application_id)` → Enables UPSERT

### 3.2 Indexes

Currently only one index:

```sql
CREATE INDEX idx_traffic_minute_bucket ON traffic_minute(bucket_utc);
```

**Missing indexes for efficient filtering:**
- ❌ `idx_traffic_minute_interface` on `(interface_id)`
- ❌ `idx_traffic_minute_network` on `(network_id)`
- ❌ `idx_traffic_minute_application` on `(application_id)`
- ❌ Composite: `(application_id, bucket_utc)` for app timeline queries
- ❌ Composite: `(network_id, bucket_utc)` for network timeline queries

---

## 4. Current Aggregation Strategy

### 4.1 In-Memory Buffer (Live Window)

```csharp
ConcurrentDictionary<DateTime, ConcurrentBag<TrafficSample>> _buckets
```

**Mechanism:**
- Every sample is keyed by `DateTime.UtcNow.Date, Hour, Minute`
- Thread-safe bag accumulates samples within same minute bucket
- Flush triggered every ~2 seconds: `FlushOlderThanAsync(currentUtc - 2min)`
- Only fully-passed buckets are flushed (avoids race conditions)

### 4.2 Aggregation Logic

For each flush cycle:

1. Snapshot bucket bags (thread-safe)
2. Remove flushed keys from dictionary
3. Group samples by `{InterfaceId, ApplicationId}`
4. Sum `BytesReceived`, `BytesSent`, count samples
5. Determine `AttributionState`:
   - `ApplicationId != null` → `Attributed`
   - `ApplicationId == null` → `Unattributed`
6. Write via **UPSERT**:
   ```sql
   INSERT INTO ... ON CONFLICT(bucket_utc, interface_id, application_id) 
   DO UPDATE SET download_bytes = ..., upload_bytes = ..., sample_count = ...
   ```

### 4.3 Conservation Invariant

**Claim (Worker.cs, lines 127-141):**
```csharp
// Strict Conservation: The remainder must be tracked as Unattributed
var remainingRx = delta.BytesReceived - attributedRx;
var remainingTx = delta.BytesSent - attributedTx;

if (remainingRx > 0 || remainingTx > 0) {
    // Add unattributed sample
}
```

✅ **Verified** - The system correctly accounts for non-attributed traffic.

### 4.4 Gaps

❌ **No hourly aggregates** - Only minute-level stored
❌ **No daily aggregates** - No long-term retention tier
❌ **Retention policy not enforced** - Old data grows indefinitely
❌ **ADR-002 promises**: 72h minutes, 30d hours, 2y days → Not implemented

---

## 5. Current Telemetry Contracts

### 5.1 ITelemetryServiceApi Contract

Defined in `ITelemetryServiceApi.cs`:

```csharp
Task<DashboardSummary> GetDashboardSummaryAsync();
Task<TrafficTimeline> GetTrafficTimelineAsync(DateTime startUtc, DateTime endUtc, string resolution);
Task<List<TopUsageEntry>> GetTopApplicationsAsync(DateTime startUtc, DateTime endUtc, int limit);
Task<List<NetworkUsage>> GetNetworkUsageAsync(DateTime startUtc, DateTime endUtc);
Task<ApplicationUsage> GetApplicationUsageAsync(string applicationId, DateTime startUtc, DateTime endUtc);
Task<List<ConnectionEvent>> GetConnectionEventsAsync(int limit);
Task<ConnectionQuality> GetConnectionQualityAsync();
Task<CurrentSnapshot> GetCurrentSnapshotAsync();
```

**Status:**
- ✅ `GetDashboardSummaryAsync` - Fully implemented
- ✅ `GetTrafficTimelineAsync` - Implemented with "1s" live mode + historical minute mode
- ✅ `GetTopApplicationsAsync` - Implemented with DB + unflushed buffer merge
- ❌ `GetNetworkUsageAsync` - NOT IMPLEMENTED (throws NotImplementedException)
- ❌ `GetApplicationUsageAsync` - NOT IMPLEMENTED (throws NotImplementedException)
- ❌ `GetConnectionEventsAsync` - NOT IMPLEMENTED (throws NotImplementedException)
- ✅ `GetConnectionQualityAsync` - Mocked (returns hardcoded "Good")
- ✅ `GetCurrentSnapshotAsync` - Returns live buffer snapshot

### 5.2 Data Models

#### `TrafficTimeline`
```csharp
public List<TrafficTimelinePoint> Points { get; set; }
```
Each `TrafficTimelinePoint`:
```csharp
public DateTime TimestampUtc { get; set; }
public long DownloadBytes { get; set; }
public long UploadBytes { get; set; }
```

**Used by:** `GetTrafficTimelineAsync` returns points grouped by minute bucket (historical) or second (live).

#### `TopUsageEntry`
```csharp
public string EntityId { get; set; }
public string DisplayName { get; set; }
public string ProcessName { get; set; }
public long DownloadBytes { get; set; }
public long UploadBytes { get; set; }
public long TotalBytes { get; set; }
public string AttributionState { get; set; }
```

**Used by:** Dashboard top applications list.

**Issue:** Currently `ApplicationId` (executable path) is reused as `ProcessName`. No actual application lookup table join.

---

## 6. Current IPC API

### 6.1 Protocol Design

**Transport:** Local Named Pipes (`NamedPipeServerStreamAcl`)

**Framing:** JSON text lines (newline-terminated)

**Security ACL:**
- ✅ Builtin Administrators: ReadWrite
- ✅ Interactive User (logged-in): ReadWrite
- ❌ NetworkSid: Deny FullControl
- ❌ AnonymousSid: Deny FullControl

**Request/Response Model:**

```json
{
  "RequestId": "guid",
  "Version": "1.0",
  "Operation": "GetDashboardSummary",
  "Payload": { /* optional */ }
}
```

**Supported Operations:**
- `GetDashboardSummary` → DashboardSummary
- `GetCurrentSnapshot` → CurrentSnapshot
- `GetConnectionQuality` → ConnectionQuality
- `GetTrafficTimeline` → TrafficTimeline
- `GetTopApplications` → List<TopUsageEntry>

### 6.2 Security Review

✅ **ACL-based access control** applied
✅ **Frame size limit** (10KB) enforced
✅ **No SQL injection risk** (Dapper parameters)
⚠️ **No authentication token** - Relies purely on OS ACL

**Risk:** If malicious local user bypasses ACL (unlikely), they could call any operation without credentials. Mitigation acceptable given OS-layer protection.

---

## 7. Current UI Architecture

### 7.1 WinUI 3 App Shell

```
MainWindow.xaml
├─ Custom Title Bar (Drag region preserved)
├─ NavigationView (Sidebar)
│  └─ Pages: Dashboard, Traffic, Applications, Networks, Sessions, Analytics
└─ Frame (Page navigation host)
```

### 7.2 Pages Currently Implemented

| Page | Status | Notes |
|------|--------|-------|
| DashboardPage | ✅ Functional | Main live view, polling every 1s |
| TrafficPage | ⚠️ Stub | XAML exists, minimal C# |
| ApplicationsPage | ⚠️ Stub | XAML exists, minimal C# |
| NetworksPage | ⚠️ Stub | XAML exists, minimal C# |
| SessionsPage | ⚠️ Stub | XAML exists, minimal C# |
| AnalyticsPage | ⚠️ Stub | XAML exists, minimal C# |

**Dashboard is LOCKED per QWEN prompt.** No visual changes allowed unless genuine defect.

### 7.3 ViewModels

Only `DashboardViewModel` is substantially implemented:

```csharp
DashboardViewModel
├─ LoadDashboardDataAsync()
├─ PollLiveDataAsync() @1s
├─ OnNavigatedFrom()
└─ Properties: Summary, Snapshot, Quality, Timeline, TopApps[]
```

**TrafficExplorerViewModel does NOT exist** - Must be created from scratch.

### 7.4 Services

| Service | Purpose |
|---------|---------|
| LiveTrafficVisualizer | Chart rendering component (XAML+C#) |
| DataStateContainer | Loading/Empty/Error state management |

---

## 8. Existing Reusable Components

### 8.1 Core Contracts (Already Exist)

✅ `DashboardSummary` - Already complete
✅ `TrafficTimeline` - Already complete
✅ `TopUsageEntry` - Already complete
✅ `TrafficTimelinePoint` - Already complete
✅ `ConnectionQuality` - Already complete
✅ `CurrentSnapshot` - Already complete
✅ `NetworkUsage` - Already defined (not used)
✅ `ApplicationUsage` - Already defined (not used)
✅ `ConnectionEvent` - Already defined (not used)

### 8.2 Monitor Components

✅ `WindowsNetworkInterfaceMonitor` - Gets interface counters via `NetworkInterface.GetIPv4Statistics()`
✅ `TrafficDeltaCalculator` - Delta calculation with reset handling
✅ `EtwKernelTraceMonitor` - ETW packet capture (requires elevated privileges)
✅ `MinuteAggregator` - In-memory buffering + batch flush to SQLite

### 8.3 Data Components

✅ `DatabaseFactory` - SQLite connection factory with WAL mode
✅ `SchemaMigrationEngine` - Creates tables if missing
✅ `SqliteTelemetryQueryService` - Implements ITelemetryServiceApi
✅ `LiveTelemetryBuffer` - Single-source-of-truth for live dashboard

### 8.4 IPC Components

✅ `IpcServer` - Named pipe server with ACL security
✅ `IpcClient` - Syncs to ITelemetryServiceApi interface
✅ `IpcRequest/IpcResponse` - Framing messages

### 8.5 UI Components (WinUI 3)

✅ `AppShell` - Main window layout
✅ `NavigationRail` - Sidebar navigation
✅ `DashboardPage` - Live dashboard
✅ `DashboardViewModel` - Dashboard binding logic
✅ `LiveTrafficVisualizer` - Real-time chart component
✅ `ComponentDataState` - Enum: Loading/Empty/Error/Normal/Stale/Offline

---

## 9. Existing Tests

### 9.1 Test Project: InternetTracer.Tests

```
MinuteAggregatorTests.cs           # 4 tests
TelemetryIntegrityTests.cs         # 2 tests
TrafficDeltaCalculatorTests.cs     # 1 test
UnitTest1.cs                       # Placeholder
```

### 9.2 Test Coverage

**Passed:** 8/8 tests

| Test Class | Focus | Coverage |
|------------|-------|----------|
| MinuteAggregatorTests | Time-bucketing, flush logic | ✅ Good |
| TelemetryIntegrityTests | Volume conservation across layers | ✅ Critical |
| TrafficDeltaCalculatorTests | Counter reset handling | ✅ Important |

### 9.3 Missing Tests

❌ **SQL queries** - No unit tests for `SqliteTelemetryQueryService`
❌ **IPC protocol** - No integration tests for server/client
❌ **Network identity** - No tests for `NetworkFingerprintGenerator`
❌ **Time boundaries** - Edge cases for day/month/year transitions
❌ **Large dataset performance** - 30-day simulated database load

---

## 10. Current Performance Risks

### 10.1 Database Queries

**Current state:** All queries use single-column index on `bucket_utc`.

**Risk scenarios:**

1. **Filter by network** → `WHERE network_id = ?` → ❌ Full table scan
2. **Filter by application** → `WHERE application_id = ?` → ❌ Full table scan
3. **Join applications table** → ❌ No index on foreign key

**Estimated impact at scale:**
- 1M rows in `traffic_minute` → 500ms query time (worst-case)
- 10M rows → Potentially 5+ seconds

**Recommendation:** Create composite indexes BEFORE implementing Traffic Explorer.

### 10.2 Memory Pressure

**Current:** Unflushed samples held indefinitely until flush.

**Risk:** If service crashes before flush (≤2 min loss acceptable).

**Long-term risk:** Dictionary `_buckets` grows without eviction policy.

**Mitigation:** Implement bounded memory cache (e.g., only keep last 15 minutes).

### 10.3 IPC Latency

**Current:** Synchronous await pattern (`_lock.WaitAsync()`).

**Risk:** Multiple concurrent UI tabs/charts → Serialized requests.

**Acceptable?** Dashboard polls at 1Hz → Max 1 request/sec → ✅ Acceptable.

**For Traffic Explorer:** Historical queries may take longer → Async cancellation tokens needed.

---

## 11. Current Technical Debt

### 11.1 ADR Violations

**ADR-002 promises:**
- ✅ 1-second raw samples (in-memory only)
- ✅ Minute aggregates stored
- ❌ Hourly aggregates (NOT IMPLEMENTED)
- ❌ Daily aggregates (NOT IMPLEMENTED)
- ❌ Retention enforcement (NOT IMPLEMENTED)

**Impact:** Database will grow linearly without bounds. Long-term scalability compromised.

### 11.2 Unused Infrastructure

- `NetworkUsage` model defined but never queried
- `ApplicationUsage` model defined but never queried
- `ConnectionEvent` model defined but never emitted
- `Analytics` project (planned) does not exist

### 11.3 Hardcoded Values

```csharp
// Worker.cs line 67: "Simple proportional attribution"
// This heuristic may misattribute traffic when ETW coverage < 100%
```

**Better approach:** Explicit attribution confidence tracking at sample level.

### 11.4 Missing Error Handling

```csharp
// SqliteTelemetryQueryService.cs lines 153-165
throw new NotImplementedException();
```

Three critical APIs stubbed out:
- `GetNetworkUsageAsync`
- `GetApplicationUsageAsync`
- `GetConnectionEventsAsync`

These ARE NEEDED for Traffic Explorer detail views.

### 11.5 Weak Attribution Logic

```csharp
// Worker.cs line 100-110
var totalEtwRx = processTraffic.Values.Sum(x => x.rx) + 1;
long attributedRx = (long)((pt.Value.rx / (double)totalEtwRx) * delta.BytesReceived);
```

**Critique:** Proportional attribution assumes ETW captures ALL traffic. Reality:
- Some processes spawn too quickly to resolve PID → Name
- Virtual adapters (VPN, WSL) may generate duplicate counts
- System idle process (PID 0) not counted in ETW

**Result:** Attributed sum ≠ Interface total → Unattributed bucket fills up

**This is honest behavior (per spec)** but should be communicated clearly to users.

---

## 12. Traffic Explorer Requirements Mapping

### 12.1 Required Features vs. Current Capability

| Feature | Status | Implementation Effort |
|---------|--------|----------------------|
| **TIME RANGE SELECTOR** | ❌ | Medium (model + UI control) |
| **HISTORICAL TIMELINE CHART** | ⚠️ Partial | Medium (`GetTrafficTimelineAsync` exists but limited) |
| **DOWNLOAD/UPLOAD/TOTAL TOGGLE** | ✅ | None (already in model) |
| **NETWORK FILTER** | ❌ | High (`GetNetworkUsageAsync` not implemented) |
| **APPLICATION FILTER** | ❌ | Medium (filter logic in ViewModel) |
| **PEAK ANALYSIS** | ❌ | Low (query MAX(DownloadBytes, UploadBytes)) |
| **COMPARISON VIEW** | ❌ | Medium (period-over-period queries) |
| **DATA TABLE BELOW CHART** | ❌ | Medium (table component + virtualization) |
| **ATTRIBUTION STATE VISIBILITY** | ✅ | Low (already in contract) |
| **LOADED STATES (loading/empty/error)** | ⚠️ | Low (re-use Dashboard patterns) |
| **LIGHT/DARK THEME SUPPORT** | ⚠️ | Medium (visual QA needed) |
| **ACCESSIBLE NAVIGATION** | ❌ | Low (keyboard focus + ARIA) |
| **REDUCED MOTION** | ❌ | Low (skip animations when flag set) |
| **RESPONSIVE DESKTOP LAYOUT** | ❌ | Medium (test multiple window sizes) |

### 12.2 Dependencies for Traffic Explorer

**Must have BEFORE starting implementation:**

1. ✅ Complete `ITelemetryServiceApi` contract (all methods implemented)
2. ✅ Create `TrafficExplorerViewModel` class
3. ✅ Create `TrafficExplorerPage.xaml` (shell layout)
4. ✅ Historical timeline chart component (extend `LiveTrafficVisualizer`)
5. ✅ Filters component (network, application, direction toggle)
6. ✅ Data table component with virtualization
7. ✅ Comparison toggle UI
8. ✅ Peak summary card component

---

## 13. Missing Capabilities

### 13.1 Backend Queries

**Critical gaps to implement FIRST:**

1. `GetNetworkUsageAsync(start, end)` → Return `List<NetworkUsage>`
2. `GetApplicationUsageAsync(id, start, end)` → Return `ApplicationUsage`
3. `GetConnectionEventsAsync(limit)` → Return `List<ConnectionEvent>`

**Additional queries needed:**

4. `GetTrafficByNetworkAsync(networkId, start, end, resolution)` → For network detail page
5. `GetPeakMetricsAsync(start, end)` → Return peak download/upload rates + timestamps
6. `ComparePeriodsAsync(thisStart, thisEnd, thatStart, thatEnd)` → Period-over-period comparison

### 13.2 Higher-Level Aggregates

**Should create additional tables BEFORE scaling past 1 month:**

1. `traffic_hourly` → SUM of minute buckets per hour
2. `traffic_daily` → SUM of minute buckets per day

**Retention policy:**
- Minute aggregates: 72 hours
- Hourly aggregates: 30 days
- Daily aggregates: Indefinite

**Implementation approach:**
- Extend `MinuteAggregator` to also emit hourly/daily aggregates after flushing minute buckets
- Scheduled migration task (runs once/day) to consolidate old minutes into hours/days

### 13.3 Indexes

**Must create BEFORE implementing Traffic Explorer:**

```sql
-- Compound indexes for filtered queries
CREATE INDEX IF NOT EXISTS idx_traffic_minute_net_bucket ON traffic_minute(network_id, bucket_utc);
CREATE INDEX IF NOT EXISTS idx_traffic_minute_app_bucket ON traffic_minute(application_id, bucket_utc);
CREATE INDEX IF NOT EXISTS idx_traffic_minute_iface_bucket ON traffic_minute(interface_id, bucket_utc);
```

**Why compound?** Most queries filter by `{network|app|interface} + time range`.

### 13.4 Time Range Model

**New domain model needed:**

```csharp
public record TimeRange
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    
    public static TimeRange Last(int hours) => ...
    public static TimeRange Days(int days) => ...
    public static TimeRange Custom(DateTime start, DateTime end) => ...
}
```

**Presets to expose in UI:**
- Live (last 60 seconds)
- 1 Hour
- 6 Hours
- 24 Hours
- 7 Days
- 30 Days
- 90 Days
- Custom (date picker)

### 13.5 Resolution Strategy

**Mapping rule (to be documented and enforced):**

| Selected Range | Resolution Source | Reason |
|----------------|-------------------|--------|
| ≤ 120 seconds | Live buffer (1s samples) | Real-time requirement |
| 2 mins - 24 hours | Minute aggregates | Balance precision vs payload size |
| 1 day - 30 days | Hour aggregates | Reduce chart point count |
| > 30 days | Day aggregates | Long-term trends only |

**Implementation decision:** Backend (`SqliteTelemetryQueryService`) owns resolution selection. UI requests `[start, end]` and lets backend choose optimal granularity.

---

## 14. Proposed Time Resolution Strategy

### 14.1 Deterministic Rule Table

```
SELECTED_RANGE_SECONDS → RESOLUTION
----------------------------------
≤ 120                  → 1-second (LiveTelemetryBuffer)
121 – 86,400 (1 day)   → 1-minute (traffic_minute)
86,401 – 2,592,000 (30d) → 1-hour (new table)
> 2,592,000            → 1-day (new table)
```

### 14.2 Resolution Enforcement

```csharp
// Proposed method signature
public async Task<TrafficTimeline> GetTrafficTimelineAsync(DateTime startUtc, DateTime endUtc)
{
    var duration = (endUtc - startUtc).TotalSeconds;
    
    if (duration <= 120) return GetLiveTimeline(startUtc, endUtc);
    if (duration <= 86400) return GetMinuteTimeline(startUtc, endUtc);
    if (duration <= 2592000) return GetHourTimeline(startUtc, endUtc);
    return GetDayTimeline(startUtc, endUtc);
}
```

### 14.3 Downsampling Policy

When requested points exceed max display density (~1000 pixels):

1. Calculate bin width: `duration_seconds / 1000`
2. Group data into bins
3. Return bin averages (sum/median)

**Example:** 7-day history = 10,080 minute samples → Show at most 1,000 points → Downsample to 6-minute bins.

---

## 15. Proposed Query Strategy

### 15.1 Traffic Query Template

```sql
SELECT 
    bucket_utc,
    SUM(download_bytes) AS DownloadBytes,
    SUM(upload_bytes) AS UploadBytes,
    CASE 
        WHEN COUNT(application_id) = COUNT(0) THEN 'Attributed'
        ELSE 'Mixed'
    END AS AttributionState
FROM traffic_minute
WHERE bucket_utc BETWEEN @Start AND @End
    AND (@NetworkId IS NULL OR network_id = @NetworkId)
    AND (@ApplicationId IS NULL OR application_id = @ApplicationId)
    AND (@InterfaceId IS NULL OR interface_id = @InterfaceId)
GROUP BY bucket_utc
ORDER BY bucket_utc ASC;
```

**Parameter strategy:** Use nullable parameters (`@NetworkId` can be NULL → no filtering).

### 15.2 Peak Analysis Query

```sql
SELECT 
    MAX(download_bytes) AS PeakDownloadBytes,
    MAX(upload_bytes) AS PeakUploadBytes,
    MAX(download_bytes + upload_bytes) AS PeakTotalBytes,
    (SELECT bucket_utc FROM traffic_minute 
     WHERE bucket_utc BETWEEN @Start AND @End 
     ORDER BY download_bytes DESC LIMIT 1) AS PeakDownloadTime,
    (SELECT bucket_utc FROM traffic_minute 
     WHERE bucket_utc BETWEEN @Start AND @End 
     ORDER BY upload_bytes DESC LIMIT 1) AS PeakUploadTime
FROM traffic_minute
WHERE bucket_utc BETWEEN @Start AND @End;
```

### 15.3 Comparison Query

```sql
WITH this_period AS (
    SELECT SUM(download_bytes) AS DL, SUM(upload_bytes) AS UL
    FROM traffic_minute WHERE bucket_utc BETWEEN @ThisStart AND @ThisEnd
),
that_period AS (
    SELECT SUM(download_bytes) AS DL, SUM(upload_bytes) AS UL
    FROM traffic_minute WHERE bucket_utc BETWEEN @ThatStart AND @ThatEnd
)
SELECT 
    this.DL, this.UL,
    that.DL, that.UL,
    CAST(this.DL AS REAL) / that.DL * 100 AS DL_PercentChange,
    CAST(this.UL AS REAL) / that.UL * 100 AS UL_PercentChange
FROM this_period this, that_period that;
```

---

## 16. Proposed Traffic Explorer Contracts

### 16.1 New Contract: `TrafficExplorerSummary`

```csharp
public class TrafficExplorerSummary
{
    public TimeRange SelectedRange { get; set; } = default!;
    public long TotalDownloadBytes { get; set; }
    public long TotalUploadBytes { get; set; }
    public long TotalBytes => TotalDownloadBytes + TotalUploadBytes;
    public double AverageDownloadBps { get; set; }
    public double AverageUploadBps { get; set; }
    public long PeakDownloadBytesPerSecond { get; set; }
    public long PeakUploadBytesPerSecond { get; set; }
    public AttributionHealthState AttributionHealth { get; set; }
}
```

### 16.2 New Contract: `TrafficComparison`

```csharp
public class TrafficComparison
{
    public TrafficExplorerSummary ThisPeriod { get; set; } = default!;
    public TrafficExplorerSummary ThatPeriod { get; set; } = default!;
    public double DownloadPercentChange { get; set; }
    public double UploadPercentChange { get; set; }
}
```

### 16.3 Extended API Method Signatures

```csharp
// Add these to ITelemetryServiceApi:

Task<TrafficExplorerSummary> GetExplorerSummaryAsync(DateTime startUtc, DateTime endUtc);
Task<TrafficComparison> ComparePeriodsAsync(DateTime start1, DateTime end1, DateTime start2, DateTime end2);
Task<List<NetworkBreakdown>> GetNetworkBreakdownAsync(DateTime startUtc, DateTime endUtc);
Task<List<ApplicationBreakdown>> GetApplicationBreakdownAsync(DateTime startUtc, DateTime endUtc);
Task<PeakMetrics> GetPeakMetricsAsync(DateTime startUtc, DateTime endUtc);
```

---

## 17. Proposed Component Architecture

### 17.1 Page Shell

```
TrafficExplorerPage
├─ TrafficExplorerHeader
│  ├─ TimeRangeSelector (preset buttons + custom date picker)
│  ├─ TrafficDirectionToggle (↓ ↑ ⇅)
│  └─ FilterBar (network dropdown, app search, attribution checkbox)
├─ PrimarySummaryCard
│  ├─ Total Traffic Display (large number)
│  ├─ Download/Upload split (stacked bar)
│  └─ Duration label ("Last 24 hours")
├─ MainTrafficChart
│  ├─ Line/Area series (Download, Upload stacked)
│  ├─ Tooltip overlay (hover)
│  └─ Scale controls (auto/manual)
├─ PeakAndAverageRow
│  ├─ PeakDownload metric
│  ├─ PeakUpload metric
│  ├─ AvgDownload metric
│  └─ AvgUpload metric
├─ BreakdownSection (collapsible)
│  ├─ Top 10 Applications table
│  └─ Top 5 Networks list
└─ ComparisonToggle
   └─ Period-over-period switch (off/on)
```

### 17.2 ViewModel Responsibilities

```
TrafficExplorerViewModel
├─ TimeRange property (binds to TimeRangeSelector)
├─ SelectedNetworkId (nullable)
├─ SelectedApplicationId (nullable)
├─ SelectedInterfaceId (nullable)
├─ TrafficDirectionMode { Download | Upload | Total }
├─ IsComparisonEnabled (bool)
├─ ComparisonPeriod { Last Day | Last Week | Last Month }
├─ Summary { get; calls IPC }
├─ TimelinePoints { get; calls IPC }
├─ PeakMetrics { get; calls IPC }
├─ RefreshCommand()
├─ CompareWithPreviousCommand()
└─ ClearFiltersCommand()
```

### 17.3 State Management

```
ComponentDataState enum:
├─ Normal (data loaded)
├─ Loading (request pending)
├─ Empty (no data in range)
├─ Error (IPC/db failure)
├─ Offline (service disconnected)
└─ Degraded (partial attribution, still show data)
```

Reuse existing Dashboard patterns for consistency.

---

## 18. Performance Strategy

### 18.1 Caching Policy

**Cache key format:**
```
telemetry:explorer:{direction}:{networkId}:{appId}:{start}:{end}:{resolution}
```

**Cache duration:** 5 seconds (reasonable freshness for 1Hz-changed data)

**Invalidate triggers:**
- Time range changed
- Filter changed
- Direction changed
- Manual refresh

**Implementation choice:** In-memory cache via `MemoryCache` in `SqliteTelemetryQueryService`.

### 18.2 Pagination Strategy

**Do NOT paginate timeline chart** - Users expect full history visibility.

Instead:
- Limit maximum historical range to 90 days (configurable in Settings)
- Enforce downsampling to ≤1,000 chart points
- Allow zoom/pan within visible range

### 18.3 Virtualized Table

**For Top Applications breakdown:**
- Use WinUI 3 `ItemsRepeater` with virtualization enabled
- Fetch only first 50 rows initially
- Load more on scroll (if needed)

---

## 19. Testing Strategy

### 19.1 New Test Categories Needed

| Category | What to Test | Priority |
|----------|--------------|----------|
| **Resolution Selection** | Verify correct granularity chosen for each range | HIGH |
| **Volume Conservation** | Confirm sums match across ranges | HIGH |
| **Filter Correctness** | Network/app/interface filter excludes correctly | HIGH |
| **Peak Detection** | MAX values match actual samples | MEDIUM |
| **Comparison Math** | Percent-change formulas accurate | MEDIUM |
| **Performance** | Query under 500ms for 30-day range | LOW (but important) |
| **Edge Cases** | Midnight, DST, leap years, empty ranges | MEDIUM |

### 19.2 Fixture Data Generation

Create deterministic fixture generator:

```csharp
public static class TrafficFixtureGenerator
{
    public static List<TrafficMinuteAggregate> GenerateLowActivityDay(DateTime date)
    public static List<TrafficMinuteAggregate> GenerateHeavyDownloadDay(DateTime date)
    public static List<TrafficMinuteAggregate> GenerateSpikyDay(DateTime date)
    public static List<TrafficMinuteAggregate> GenerateMultiAppDay(DateTime date)
}
```

Use fixtures for:
- Unit tests (fast, no database)
- Integration tests (seed real DB then query)
- Performance tests (scale fixture to 1M rows)

### 19.3 Visual Regression Testing

Capture screenshots of:
- Traffic Explorer at 1280×720
- Traffic Explorer at 1920×1080
- Traffic Explorer at 2560×1440
- Light theme variant
- Dark theme variant (default)

Baseline images established → Future PRs compared against baseline.

---

## 20. Migration Risk

### 20.1 Database Schema Changes

**Risk Level:** LOW (backward compatible)

**Changes:**
- Add `traffic_hourly` table (new table, no existing data affected)
- Add `traffic_daily` table (new table, no existing data affected)
- Add indexes to `traffic_minute` (non-destructive)

**Rollback plan:** If migration fails, keep existing schema → Run service in degraded mode (slower queries).

### 20.2 Code Migration

**Risk:** Introducing new dependencies could break existing Dashboard polling loop.

**Mitigation:**
- Isolate Traffic Explorer logic in separate namespace (`InternetTracer.TrafficExplorer`)
- Do NOT modify DashboardViewModel
- Shared contracts (e.g., `TrafficTimeline`) remain unchanged

### 20.3 IPC Contract Extension

**Risk:** Adding new operations breaks backward-compatible clients.

**Mitigation:**
- Version IPC protocol (`Version: "1.1"` for new ops)
- Server validates client version before accepting new operations
- Older clients continue using v1.0 operations

---

## 21. Dashboard Regression Risk

### 21.1 Confirmed Safe Areas

✅ **DashboardViewModel** - Completely isolated, no shared state with Traffic Explorer
✅ **LiveTelemetryBuffer** - Single source for live data, read-only by both components
✅ **IPC Server** - Adds new operations without removing old ones

### 21.2 Potential Conflict Zones

⚠️ **Common Contracts** - Traffic Explorer adds new fields? NO. Stick to existing shapes.
⚠️ **Shared Styles** - Traffic Explorer reuses DesignSystem.xaml → Ensure styles additive
⚠️ **Navigation** - Traffic Explorer added to sidebar? Does not affect Dashboard page

### 21.3 QA Checklist for Dashboard

Before merging Traffic Explorer PR:

1. Launch UI → Verify Dashboard loads
2. Wait 10 seconds → Verify live polling continues
3. Switch networks (if possible) → Verify dashboard updates
4. Close UI → Restart UI → Verify quick load time
5. Check light mode → Verify colors legible
6. Keyboard tab through → Verify focus indicators
7. Resize window to compact (1024×768) → Verify no overlap

---

## 22. Recommended Implementation Sequence

### Phase K16.1: Architecture Foundation (2-3 days)

1. ✅ **Repository reconnaissance COMPLETE** (this document)
2. ⏳ Create `TrafficExplorerViewModel` (stub with mock data)
3. ⏳ Create `TrafficExplorerPage.xaml` skeleton (header, summary, chart placeholder)
4. ⏳ Add index creation to `SchemaMigrationEngine.cs`
5. ⏳ Implement missing `ITelemetryServiceApi` methods (stub for now)

### Phase K16.2: Historical Query Infrastructure (3-4 days)

1. ⏳ Implement `GetExplorerSummaryAsync` with real DB queries
2. ⏳ Implement `GetTrafficTimelineAsync` with resolution selection
3. ⏳ Implement `GetPeakMetricsAsync`
4. ⏳ Add volume conservation tests
5. ⏳ Performance benchmark (1M row dataset)

### Phase K16.3: UI Implementation (4-5 days)

1. ⏳ Build `TimeRangeSelector` component
2. ⏳ Build `TrafficDirectionToggle` component
3. ⏳ Build `FilterBar` component (network/app/interface)
4. ⏳ Extend `LiveTrafficVisualizer` for historical mode
5. ⏳ Build `PeakAndAverageRow` component
6. ⏳ Build `BreakdownTable` component (virtualized)

### Phase K16.4: Polish and QA (2-3 days)

1. ⏳ Accessibility audit (keyboard, screen reader)
2. ⏳ Responsive design validation (3 window sizes)
3. ⏳ Light/dark theme visual QA
4. ⏳ Reduced motion compliance
5. ⏳ Regression testing on Dashboard

### **Total Estimated Effort: 11-15 workdays**

---

## 23. Open Technical Uncertainties

### 23.1 ETW Coverage Accuracy

**Unknown:** Does ETW kernel provider capture **100%** of TCP/IP traffic on all Windows builds?

**Risk:** If ETW misses significant packets, attribution ratios drift unpredictably.

**Mitigation:** Document attribution uncertainty in UI. Never claim exact attribution.

### 23.2 Sleep/Resume Counter Resets

**Unknown:** How often do interface counters reset after sleep/wake cycles?

**Risk:** Could produce false "giant spike" artifacts in traffic_timeline.

**Mitigation:** Current `TrafficDeltaCalculator` handles negatives as zeros. Acceptable loss for short interval.

### 23.3 VPN Adapter Double Counting

**Unknown:** Will physical interface AND VPN adapter both report the same bytes?

**Risk:** Inflated totals if both included in aggregation.

**Mitigation:** User configures which interfaces to include/exclude in Settings. Default exclude virtual adapters.

### 23.4 High-DPI Rendering

**Unknown:** Will WinUI 3 charts render crisp lines at 150%/200% DPI?

**Risk:** Blurry graphics → Professional quality perception damage.

**Mitigation:** Validate visually at each DPI setting during QA. Adjust font sizes/line thickness accordingly.

---

## 24. PASS / FAIL / NOT PROVEN Assessment

### 24.1 Pass Criteria

| Criteria | Status | Evidence |
|----------|--------|----------|
| **Build Compiles** | ✅ PASS | `dotnet build` succeeded with 0 errors |
| **Tests Pass** | ✅ PASS | 8/8 unit tests passed |
| **Architecture Documented** | ✅ PASS | 7 ADRs exist covering major decisions |
| **Contracts Defined** | ✅ PASS | `ITelemetryServiceApi` complete except 3 stubbed methods |
| **IPC Working** | ✅ PASS | IpcServer/IpcClient implemented with ACL |
| **Core Collection** | ✅ PASS | Monitor collects interface counters + ETW |
| **Aggregation** | ✅ PASS | MinuteAggregator flushes to SQLite |
| **Database Exists** | ✅ PASS | SchemaMigrationEngine creates tables |

### 24.2 Fail Criteria (Blockers)

| Criteria | Status | Evidence |
|----------|--------|----------|
| **All Contracts Implemented** | ❌ FAIL | `GetNetworkUsageAsync`, `GetApplicationUsageAsync`, `GetConnectionEventsAsync` throw `NotImplementedException` |
| **Hourly/Daily Aggregates** | ❌ FAIL | ADR-002 promises not implemented |
| **Retention Policy** | ❌ FAIL | No cleanup mechanism exists |
| **Historical Query Performance** | ❓ NOT PROVEN | No benchmarks with realistic data volumes |
| **High-DPI Rendering** | ❓ NOT PROVEN | No visual validation completed |
| **Virtual Adapter Handling** | ❓ NOT PROVEN | No testing performed |

### 24.3 Overall Readiness: **PARTIALLY READY FOR K16**

**What's Ready:**
- ✅ Can start K16.1 (ViewModel/Page skeleton) immediately
- ✅ Can start adding missing API implementations
- ✅ Can add indexes to improve query performance

**What's Blocking:**
- ❌ Cannot implement detailed network/application breakdowns without `GetNetworkUsageAsync`
- ❌ Cannot guarantee long-term scalability without retention/enforcement
- ❌ Cannot validate performance without benchmarks

**Recommendation:**
Proceed with **incremental rollout**:
1. Implement missing API methods (blocking blocker)
2. Add indexes (low-risk improvement)
3. Build Traffic Explorer MVP (basic timeline + filters)
4. Defer complex features (comparison, advanced breakdowns) until later iteration
5. Plan K17/K18 for retention/enforcement/higher-aggregates

---

## Appendix A: File Locations Reference

| Component | File Path(s) |
|-----------|--------------|
| **Core Models** | `InternetTracer.Core.Models/*` |
| **Contracts** | `InternetTracer.Core.Contracts/*.cs` |
| **Data Layer** | `InternetTracer.Data/*.cs` |
| **Monitor Layer** | `InternetTracer.Monitor/*.cs` |
| **IPC Layer** | `InternetTracer.Ipc/*.cs` |
| **Service** | `InternetTracer.Service/Worker.cs` |
| **UI Shell** | `InternetTracer.App/MainWindow.xaml.cs` |
| **Dashboard** | `InternetTracer.App/Views/DashboardPage.xaml{,.cs}` |
| **ViewModels** | `InternetTracer.App/ViewModels/*.cs` |
| **Tests** | `InternetTracer.Tests/*Tests.cs` |
| **ADRs** | `ADRs/ADR-*.md` |

---

## Appendix B: Terminology Clarifications

| Term | Meaning |
|------|---------|
| **Volume** | Total bytes transferred (MB, GB) |
| **Rate** | Bytes per second (MB/s, Mbps) |
| **Attributed** | Traffic confidently linked to a specific application |
| **Unattributed** | Traffic not linked to any known process (OS overhead, unknown PIDs) |
| **Bucket** | Time-aligned window (1 minute = 60 seconds aggregated together) |
| **Resolution** | Granularity of returned data (1s, 1m, 1h, 1d) |
| **Downsampling** | Reducing point count while preserving overall shape (bin averaging) |
| **FLUSH** | Writing in-memory buffer to SQLite disk |

---

**END OF REPORT**

---

**Next Action:** Begin K16.1 implementation sequence by creating `TrafficExplorerViewModel` with mock data, then iterating toward real IPC integration.
