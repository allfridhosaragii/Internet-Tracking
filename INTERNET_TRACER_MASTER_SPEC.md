# INTERNET TRACER
## Master Product, UX, UI, Architecture, Engineering, Performance, Privacy, and QA Specification

Version: 1.0
Target platform: Windows 11 desktop, Windows 10 compatibility where technically practical
Default theme: Dark
Storage model: Local-only by default
Primary purpose: Long-running local network traffic measurement, attribution, history, analytics, and visualization

---

# 0. INSTRUCTIONS TO THE AI AGENT

You are the principal software architect, product engineer, UI/UX director, performance engineer, and QA engineer for Internet Tracer.

This document is the source of truth for the project unless an explicit user decision supersedes it.

Do not start by generating a large amount of code. First inspect the repository, identify the actual project state, validate technical assumptions against the host OS and SDK, and propose an implementation sequence. Preserve working code unless a change is necessary and justified.

Before making a UI change, perform a layout impact check:
1. Identify the visual region being changed.
2. Identify all neighboring regions and minimum usable widths/heights.
3. Check whether the proposed change can overlap, cause clipping, create scroll traps, or break the title bar, navigation, filters, charts, tables, or window controls.
4. Check light and dark themes.
5. Check compact and wide window sizes.
6. Check keyboard focus and accessibility.
7. Check animation impact on CPU/GPU and reduced-motion behavior.
8. Re-check the complete page after the change.

Repeat this reasoning for every substantial UI modification. Do not optimize one component while silently degrading another.

Do not accept visual quality just because the code compiles. A compiled interface can still be visually poor, generic, crowded, or inconsistent.

Do not use generic AI-generated dashboard patterns. Internet Tracer must look like a deliberately designed desktop product with its own visual language.

Do not copy the visual identity of an existing commercial product. Use established desktop design principles as foundations, then create an original Internet Tracer system.

Do not use fake data in production views. Mock data is allowed only in isolated development fixtures and visual QA.

Do not silently weaken requirements to make tests pass. Distinguish implementation bugs, test-harness bugs, environment limitations, and real product limitations.

When uncertain about a platform-specific capability, verify the actual API documentation and, when necessary, build a small proof of concept before committing the architecture.

Never claim that the product is 100% accurate at process attribution unless the collection method has been experimentally verified on the target Windows version. The UI must disclose attribution uncertainty when it exists.

Never collect packets, payloads, domains, or application content by default when the product requirement can be fulfilled with counters and telemetry that contain less sensitive information.

---

# 1. PRODUCT VISION

Internet Tracer is a local-first Windows application that continuously measures the laptop's network usage and transforms it into useful historical information.

The core question the product must answer is:

"Where is my internet going?"

The application should answer all of the following:

- How much data did my laptop download?
- How much did it upload?
- How much was used in the current second, minute, hour, day, week, month, and year?
- Which application or process consumed the most data?
- Which applications are active versus operating in the background?
- Which network was being used at the time?
- How much did each network contribute to the total?
- When did the laptop switch networks?
- Which hours and days had the highest traffic?
- What were the observed download and upload speeds?
- What were the measured latency, jitter, and packet-loss characteristics when active probing is enabled?
- How stable was the connection?
- What changed compared with the previous period?
- What is the estimated usage for the rest of the selected period?
- Why did a hotspot or another metered connection consume data so quickly?

Internet Tracer is an observability product for the user's own laptop, not a packet-sniffing surveillance tool.

---

# 2. NON-NEGOTIABLE PRODUCT PRINCIPLES

1. Local-first
All core measurement, aggregation, storage, analytics, search, and visualization run locally.

2. Privacy-first
No account, cloud database, remote analytics, or telemetry is required for core functionality.

3. Transparent
Every metric should have a clear definition, unit, scope, and time range.

4. Lightweight
The monitor service should remain low CPU, low memory, and low disk-write overhead during idle periods.

5. Long-running
The application must be capable of running continuously for months or years without uncontrolled database growth.

6. Evidence-based attribution
Application traffic is shown as measured or inferred traffic. The system must not pretend to know more than the collector actually knows.

7. Desktop-native behavior
Windowing, keyboard input, focus, title bar, menus, notifications, system startup, resizing, and accessibility should feel appropriate for Windows 11.

8. Strong visual identity
Internet Tracer should be recognizable from its interface alone.

9. Data density without clutter
The application can display a lot of information, but hierarchy must prevent information overload.

10. Motion with meaning
Animations must explain change, state, continuity, or hierarchy. Decorative motion should be rare.

11. Accessibility is part of design quality
Contrast, keyboard navigation, visible focus, readable text, and reduced-motion behavior are requirements, not optional polish.

12. No regression from local improvements
Every significant change must be reviewed against the entire shell, page, theme, and interaction model.

---

# 3. SCOPE OF VERSION 1

## 3.1 Required MVP

- Windows desktop application
- Background monitoring service/worker
- Automatic start with Windows after installation/configuration
- Local SQLite database
- Network interface traffic collection
- Download and upload byte accounting
- Real-time traffic display
- Per-second live visualization
- Minute/hour/day/week/month/year aggregation
- Network identity and network switching history
- Wi-Fi SSID/BSSID-aware identity where available
- Application/process attribution where supported
- Active versus background process classification
- Top 5 download, upload, and combined usage
- History charts
- Tables with filtering
- Dashboard
- Traffic page
- Applications page
- Networks page
- Sessions page
- Speed/Connection page
- History/Analytics page
- Alerts page
- Settings page
- Data export
- Local backup/restore
- Dark mode default
- Light mode
- Reduced motion option
- Search
- Empty states, loading states, error states, degraded-data states

## 3.2 Post-MVP candidates

- Advanced process attribution using ETW or other verified system tracing mechanisms
- Application grouping by vendor/category
- Per-network quotas
- Metered-network warnings
- Hotspot mode
- Smart anomaly detection
- Usage forecasting
- Data budget management
- Automatic database archival/compression
- Optional domain-level telemetry, only as an explicit opt-in feature with a strong privacy explanation
- Import/export across devices, still local-file based
- Portable mode
- Installer/update channel

---

# 4. PLATFORM AND TECHNICAL DIRECTION

Recommended primary stack for Windows-first development:

- C#
- .NET 10 LTS-compatible target where practical, or current supported .NET version validated against the project environment
- Windows App SDK / WinUI 3 for native Windows desktop UI
- SQLite for local persistence
- MVVM or an equivalent strict separation between UI state and domain/services
- Windows Service or a well-validated background worker strategy for continuous monitoring
- ETW and native Windows networking APIs where process-level attribution requires deeper telemetry
- Windows App SDK application notifications for user-facing alerts

Why this direction:

Windows provides native desktop primitives for NavigationView, custom title bars, Mica materials, app notifications, background/service integration, and Windows networking APIs. WinUI 3 also gives access to the Windows Composition system for smooth animation.

Important: do not force all work into one process. A monitoring collector and a UI application have different lifecycle and reliability needs.

Preferred logical split:

InternetTracer.App
UI shell, navigation, user interaction, charts, settings, data explorer

InternetTracer.Core
Domain models, units, aggregation rules, configuration, business logic

InternetTracer.Monitor
Long-running collectors, network state, interface counters, process telemetry, session detection

InternetTracer.Data
SQLite schema, repositories, migration engine, query layer

InternetTracer.Analytics
Top-N queries, trend calculations, forecasting, comparison, health scoring

InternetTracer.Infrastructure
Windows API adapters, ETW adapters, notifications, startup integration, file system, diagnostics

InternetTracer.Tests
Unit tests, integration tests, deterministic fixtures

InternetTracer.UiTests
UI automation and screenshot/visual regression tests where supported

Keep platform-specific code behind interfaces so the domain is testable without a live network.

---

# 5. CRITICAL DATA-COLLECTION MODEL

## 5.1 Interface-level counters

The primary byte-usage measurement should use system network interface counters, not packet capture.

For every monitored interface collect at least:

- timestamp
- interface identifier
- interface name
- interface type
- operational state
- bytes received
- bytes sent

Calculate traffic deltas between samples.

Example:

Sample A
RX = 100000000000
TX = 20000000000

Sample B
RX = 100200000000
TX = 20010000000

Delta
RX = 200 MB
TX = 10 MB

Use 64-bit or larger counters and handle counter wrap/reset correctly.

Do not store a separate database row for every byte or packet.

## 5.2 Sampling

Default service sampling target:

- Normal background: approximately 1 second logical cadence for real-time measurements.
- Database writes: aggregate samples into minute buckets instead of writing an unbounded raw row every second.
- Live UI: subscribe to an in-memory stream or IPC channel rather than polling SQLite every second.

The exact implementation can use sub-second system timers if required for stability, but do not use unnecessarily aggressive sampling.

The database should store high-resolution data only when the retention policy requires it.

Recommended retention:

- 1-second observations: 24 to 72 hours
- 1-minute aggregates: at least 180 days
- 1-hour aggregates: at least 2 years
- 1-day aggregates: indefinite by default

Make retention configurable.

## 5.3 Aggregate dimensions

Every aggregate should be queryable by:

- time bucket
- interface
- network identity
- process/application identity where available
- traffic direction
- scope/category

Traffic direction:

DOWNLOAD = bytes received
UPLOAD = bytes sent
TOTAL = download + upload

## 5.4 Do not confuse speed and volume

Volume is the amount of data transferred.

Speed is the rate of transfer.

Example:

100 MB downloaded over 10 seconds = 10 MB/s average.

Never display a volume metric using speed units or vice versa.

---

# 6. PROCESS / APPLICATION TRAFFIC ATTRIBUTION

Process attribution is an advanced part of Internet Tracer and must be implemented carefully.

A normal interface counter can tell the total traffic crossing the interface. It does not automatically provide perfect per-process accounting.

Where detailed process attribution is required, use a verified Windows tracing/telemetry method, such as ETW-based TCP/IP events or another supported mechanism appropriate to the actual Windows build.

Microsoft documents that the TCP/IP ETW provider can expose network events with ProcessId information, while also warning that some events are emitted by separate threads and therefore process identification is not universally perfect.

Therefore the architecture must support these states:

ATTRIBUTED
Traffic confidently associated with a process.

PARTIALLY_ATTRIBUTED
Some activity associated, some unknown.

UNATTRIBUTED
Traffic observed at interface level but not confidently mapped to a process.

FAILED
The collector could not operate or permissions/environment prevented collection.

The UI must never silently add unknown traffic to a random application.

Show an "Unattributed" bucket when necessary.

This bucket is a feature, not an error, because it preserves accounting integrity.

## 6.1 Process fields

- PID
- executable name
- normalized display name
- executable path when permission permits
- parent PID when useful
- process start time
- process end time
- active/background classification
- publisher/vendor where locally derivable
- measured download bytes
- measured upload bytes
- confidence/attribution state

## 6.2 Application identity

A process restart must not necessarily be treated as a completely different application in historical charts.

Persist a stable application identity derived from validated fields such as executable path and normalized executable identity.

Never use the PID as the long-term application identity.

## 6.3 Active versus background

Display two concepts separately:

Foreground/active process = process with a user-visible foreground role according to Windows state.

Background process = process running without being the foreground application.

Do not imply that background means unimportant. Background services can consume large amounts of bandwidth.

---

# 7. NETWORK IDENTITY

A network must not be identified solely by its current IP address.

IP addresses can change and unrelated networks can use the same private ranges.

Preferred identity inputs, depending on availability:

- interface GUID
- SSID
- BSSID
- gateway
- subnet/prefix
- connection profile identifiers
- network category
- adapter identity
- wired network characteristics

For Wi-Fi, BSSID is especially useful for identifying the access point/BSS. Use SSID as a human-readable name, not as the only canonical identity.

Create a NetworkFingerprint object with:

- networkId
- displayName
- connectionType
- ssid nullable
- bssid nullable
- gateway nullable
- subnet nullable
- interfaceGuid nullable
- firstSeen
- lastSeen

Do not store Wi-Fi passwords or credentials.

Treat externally supplied network information as untrusted data. Do not execute or deserialize arbitrary content received from network metadata.

## 7.1 Renamed SSID

If the visible SSID changes but all stronger identity attributes indicate the same network, the UI can show a continuity relationship but must not silently merge conflicting identities.

Allow the user to rename a network locally without altering the raw observed metadata.

## 7.2 Network switching

Every network transition generates an event:

NETWORK_CONNECTED
NETWORK_DISCONNECTED
NETWORK_CHANGED
NETWORK_RECONNECTED

Example:

08:12:03 Home Wi-Fi
10:41:17 Mobile Hotspot
13:18:41 Campus Wi-Fi
16:02:09 Mobile Hotspot
19:22:30 Home Wi-Fi

---

# 8. SESSION MODEL

A session represents a continuous monitoring interval on a network.

Session fields:

- sessionId
- networkId
- interfaceId
- startTime
- endTime nullable
- duration
- totalDownload
- totalUpload
- peakDownloadRate
- peakUploadRate
- averageDownloadRate
- averageUploadRate
- interruptionCount
- quality score where supported

The session view should allow drill-down into:

Network
Time interval
Traffic
Top applications
Connection events
Speed history
Quality metrics

---

# 9. SPEED AND CONNECTION QUALITY

Internet Tracer must clearly distinguish passive observation from active testing.

Passive speed:
Calculated from actual transferred bytes over time.

Active speed test:
An optional user-initiated test that intentionally generates network traffic. This must never run silently.

Latency/jitter/packet loss:
These generally require probes or protocol observations. Provide an explicit setting for active measurement and disclose that probing itself consumes a small amount of data.

Suggested active test options:

- off
- manual only
- periodic while plugged in

Never enable aggressive continuous probing by default.

Connection health should be derived from available evidence. Do not create fake precision.

Example states:

Excellent
Good
Fair
Poor
Offline
Unknown

An "Unknown" state is preferable to inventing a score when the data is insufficient.

---

# 10. DATA MODEL

Recommended core entities:

Network
NetworkAlias
NetworkSession
NetworkInterface
Process
Application
ProcessObservation
TrafficSample
TrafficMinuteAggregate
TrafficHourAggregate
TrafficDayAggregate
ConnectionEvent
SpeedSample
QualitySample
AlertRule
AlertEvent
AppSettings
DatabaseMetadata
SchemaMigration

Use UTC timestamps internally. Convert to local timezone only at presentation boundaries. Store timezone metadata for human-readable reports when useful.

## 10.1 Example normalized tables

networks
- id
- fingerprint_hash
- display_name
- ssid
- bssid
- gateway
- subnet
- connection_type
- first_seen_utc
- last_seen_utc

network_sessions
- id
- network_id
- interface_id
- start_utc
- end_utc
- download_bytes
- upload_bytes
- peak_download_bps
- peak_upload_bps

interfaces
- id
- system_guid
- name
- type
- description
- first_seen_utc
- last_seen_utc

applications
- id
- stable_key
- display_name
- executable_name
- executable_path
- publisher
- first_seen_utc
- last_seen_utc

traffic_minute
- bucket_utc
- network_id nullable
- interface_id
- application_id nullable
- download_bytes
- upload_bytes
- sample_count
- attribution_state

connection_events
- id
- timestamp_utc
- event_type
- network_id nullable
- interface_id nullable
- metadata_json nullable

Keep JSON metadata bounded and never use it to replace stable relational fields required for analytics.

---

# 11. DATABASE ENGINEERING

SQLite is the default local store.

Requirements:

- migrations
- transaction boundaries
- WAL mode where appropriate
- indexes for time-series queries
- foreign keys
- integrity checks
- graceful recovery from interrupted writes
- database backup
- schema versioning
- bounded logging

Never hold the database open from the UI for long-running write transactions.

Use batch inserts and aggregation.

Avoid N+1 queries.

Do not execute expensive full-history queries on the UI thread.

All long-running queries must be async and cancellable where possible.

Suggested indexes:

- traffic_minute(bucket_utc)
- traffic_minute(application_id, bucket_utc)
- traffic_minute(network_id, bucket_utc)
- network_sessions(start_utc)
- network_sessions(network_id, start_utc)
- connection_events(timestamp_utc)
- applications(stable_key)

Implement database health diagnostics:

- file size
- free page count
- oldest raw data
- aggregate coverage
- last successful write
- last successful integrity check

---

# 12. RETENTION AND STORAGE MANAGEMENT

Internet Tracer is intended to run for years.

Do not allow raw high-resolution observations to grow without control.

Use tiered retention:

Tier A: high resolution
Short retention.

Tier B: minute aggregates
Medium/long retention.

Tier C: hourly aggregates
Long retention.

Tier D: daily aggregates
Long-term history.

When removing Tier A data, first verify that lower-resolution aggregates cover the same time range.

Never silently delete user history. Show the retention policy clearly and allow export before destructive cleanup.

---

# 13. PRIVACY MODEL

Default:

LOCAL ONLY

The application must function without:

- account creation
- cloud sync
- remote database
- ad SDK
- analytics SDK
- telemetry service
- remote AI API

Do not include external analytics libraries by default.

Do not upload data for crash reporting without an explicit future opt-in policy.

Data that may be stored locally:

- aggregate byte counts
- application/process names
- timestamps
- network names and available network metadata
- interface metadata
- connection state
- locally configured aliases

Sensitive telemetry that is not needed for the core product must not be collected.

The UI should have a Privacy page showing:

Data storage: Local
Cloud upload: Disabled
Telemetry: Disabled
Account: Not required
Network content inspection: Disabled by default

If future features require more sensitive observation, make them explicit opt-in modules with separate consent and visual status.

---

# 14. SECURITY PRINCIPLES

Internet Tracer is monitoring its own machine, but it still has a security-sensitive surface because it may run continuously and potentially with elevated permissions for some collection techniques.

Rules:

- Minimize privilege.
- Do not run the entire UI as administrator.
- Separate privileged collection capability from the unprivileged UI where possible.
- Validate all IPC messages.
- Authenticate and authorize local IPC endpoints.
- Restrict named pipes/sockets to the intended local user/service relationship.
- Protect configuration files from untrusted modification.
- Do not load arbitrary executable modules from writable user folders.
- Pin package versions and audit dependencies.
- Never store credentials.
- Never expose a debugging API on 0.0.0.0 by default.
- Disable production verbose logging that can leak file paths or sensitive environment data.
- Escape UI-rendered process names and network metadata.

The service must remain functional when the UI is closed.

The UI must remain usable even if advanced process attribution fails.

---

# 15. STARTUP AND BACKGROUND OPERATION

The core collector should start automatically after Windows startup once enabled.

A Windows service can use auto-start or delayed auto-start where appropriate. Delayed auto-start is useful when reducing boot contention matters more than collecting the first seconds of boot traffic.

Requirements:

- Startup enabled by default during normal installation only when clearly disclosed.
- User can disable startup in Settings.
- Collector survives UI close.
- Collector recovers from transient errors.
- Collector does not create repeated crash/restart loops.
- Collector records service health.
- UI shows whether monitoring is currently active.

Tray behavior, if implemented:

- left click: open dashboard
- right click: compact menu
- quick status: current network, current download/upload rate
- option to pause monitoring
- option to quit UI

Quitting the UI must not automatically stop the collector unless explicitly chosen.

---

# 16. INFORMATION ARCHITECTURE

Primary navigation:

1. Dashboard
2. Traffic
3. Applications
4. Networks
5. Sessions
6. Speed & Quality
7. History
8. Analytics
9. Alerts
10. Data
11. Settings

Do not expose every minor setting as a first-class navigation item.

The sidebar should remain simple and scannable.

Recommended grouping:

MONITOR
Dashboard
Traffic

ANALYZE
Applications
Networks
Sessions
Speed & Quality
History
Analytics

SYSTEM
Alerts
Data
Settings

The exact grouping can be changed only if user testing shows a better information architecture.

---

# 17. UI/UX DESIGN DIRECTION

## 17.1 Overall visual character

Internet Tracer should feel:

- premium
- calm
- technical
- precise
- modern
- data-centric
- original
- desktop-native
- controlled

It should not feel:

- like a generic SaaS dashboard
- like a WordPress admin panel
- like a template marketplace dashboard
- like a crypto trading app
- like a cyberpunk game
- like a neon hacker interface
- like a generic AI-generated glassmorphism concept

## 17.2 Glassmorphism policy

Glassmorphism is a material language, not an excuse to make every surface translucent.

Use glass selectively for:

- shell surfaces
- floating toolbars
- popovers
- compact command surfaces
- transient panels
- selected overlays

Use solid or near-opaque surfaces for:

- dense tables
- dense chart regions when transparency harms readability
- forms
- complex data grids
- modal content that requires strong separation

Keep text contrast high.

Do not stack multiple translucent layers over each other unnecessarily.

Do not use excessive blur behind critical numeric content.

The visual hierarchy should still work when the blur or transparency is reduced.

## 17.3 Windows-native material influence

The Windows platform provides Mica and Acrylic materials with specific intended uses. Internet Tracer should borrow the principle of material hierarchy rather than blindly recreating a generic web glass effect.

Primary window:
Mica-like depth or an equivalent performant desktop backdrop.

Transient surfaces:
Acrylic-like treatment where appropriate.

Blocking overlays:
Smoke/dim layer or an equivalent modal scrim.

The app must remain legible when the system backdrop is unavailable.

## 17.4 Dark mode

Dark is the default.

The background should be deep neutral rather than pure black everywhere.

Suggested conceptual layers:

Background
Surface 1
Surface 2
Elevated Surface
Glass Surface
Border
Primary Text
Secondary Text
Muted Text
Accent
Positive
Warning
Critical
Info

Do not select colors by arbitrary neon fashion. Use a restrained accent palette.

The accent color is allowed to have a spectral or signal-inspired character, but it must remain readable and coherent.

## 17.5 Light mode

Light mode must be a separately validated theme, not simply dark colors inverted.

Preserve hierarchy and semantics.

Avoid low-contrast gray-on-white cards.

Ensure charts remain readable without relying on glow.

## 17.6 Color semantics

Color must not be the only signal for a state.

Example:

Good
icon + label + color

Critical
icon + label + color + optional text explanation

Download and upload should have distinguishable visual encoding and also text/icon labels.

## 17.7 Typography

Use a modern, highly readable system/Windows-compatible font family.

Preferred direction:
Segoe UI Variable or another validated Windows-native variable font.

Use tabular numbers for dashboards where alignment matters.

Numeric metrics should use:

- strong weight hierarchy
- consistent units
- predictable decimal precision
- alignment that makes comparison easy

Do not make every number oversized.

Use large numerals only for the few most important KPIs.

## 17.8 Spacing

Use a consistent spacing system based on small multiples of 4 or 8, while allowing compact controls to use smaller spacing when necessary.

Do not create arbitrary margins per component.

Recommended base rhythm:
4, 8, 12, 16, 20, 24, 32, 40

Validate actual density visually.

---

# 18. APPLICATION SHELL

The window shell should use a modern Windows desktop structure:

Title bar
Sidebar/navigation
Page header
Page content
Optional status region

The title bar must not overlap interactive navigation controls.

If using a custom title bar, reserve a safe drag region and preserve the system caption button area.

The left navigation should be visually integrated with the material backdrop but remain legible as a stable navigation anchor.

Default window behavior:

- sensible minimum size
- remembers last window size and position
- supports maximize/restore
- does not create off-screen windows
- preserves page state when switching navigation

Recommended starting size:
approximately 1440 x 900 desktop viewport, while supporting smaller windows through responsive layout rules.

Do not hard-code a single screen size.

---

# 19. GLOBAL DESIGN SYSTEM

Create tokens before building pages.

Tokens include:

- color
- background
- surface
- border
- text
- elevation
- radius
- spacing
- typography
- icon size
- chart dimensions
- motion duration
- motion easing
- opacity
- blur

## 19.1 Border radius

Avoid making every object a large rounded pill.

Use a small set of radii:

Small: 6-8px
Medium: 10-12px
Large: 16-20px
Pill: reserved for tags/compact controls

Use square/low-radius treatment for dense tables and technical areas when it improves readability.

## 19.2 Borders

Prefer subtle 1px borders over heavy drop shadows.

Glass surfaces should use low-contrast borders to define their edges.

## 19.3 Shadows

Use restrained depth.

Do not apply a large shadow to every card.

Elevation should communicate hierarchy, not decorate every object.

---

# 20. MOTION SYSTEM

Motion is part of the product language.

Every animation must have a reason:

- explain a change
- preserve spatial continuity
- signal state
- provide feedback
- direct attention

Do not animate every component on page load.

Recommended timing vocabulary:

Instant: 0ms
Micro interaction: 50-150ms
Standard transition: 150-300ms
Large panel/modal: 250-400ms

Prefer ease-out for elements entering.
Prefer ease-in for elements leaving.
Prefer ease-in-out for repositioning or transformation within the same context.

Avoid long elastic/bouncy animation for professional monitoring workflows.

## 20.1 Dashboard motion

The live traffic graph should update continuously but not visibly redraw the entire chart in a distracting way.

Use a smooth sliding time window.

KPI numeric updates:
Use subtle interpolation only when it helps the eye track a changing value.

Top 5 ranking changes:
Animate row movement with a short spatial transition rather than instantly reordering.

Network change:
Display a compact connection handoff indicator with a short transition.

No perpetual decorative particle field.

## 20.2 Chart motion

Charts should support data continuity.

Do not animate from zero every time the user changes a filter.

When filtering:

- preserve shared x-axis context when possible
- transition series opacity or position
- avoid confusing object morphs

## 20.3 Reduced motion

Honor the Windows reduced-motion/accessibility preference or provide a dedicated reduced-motion setting where platform integration is insufficient.

Reduced motion should:

- remove or shorten non-essential movement
- preserve instant feedback
- preserve state information
- avoid auto-scrolling effects

---

# 21. ICONOGRAPHY

Use one coherent icon family.

Do not mix icons from unrelated visual languages.

Icons should be:

- simple
- recognizable
- consistent stroke/fill logic
- aligned to text baseline
- used with labels when ambiguity exists

Do not use emoji as primary product icons.

---

# 22. PAGE SPECIFICATION: DASHBOARD

Dashboard is the first page and the main daily-use surface.

Purpose:
Answer the current status question within seconds.

Suggested hierarchy:

Top area:
Current network
Monitoring status
Time range selector
Refresh/live status

Primary metrics:

Total today
Download today
Upload today
Current download rate
Current upload rate

Main visual:

Live Traffic Timeline

Secondary area:

Top 5 Applications
Top 5 Download
Top 5 Upload

Lower area:

Current Network Session
Network Quality
Recent Network Changes

## Dashboard controls

Time selector:

Live
1h
6h
24h
7d
30d
90d
Custom

Traffic selector:

Total
Download
Upload

Network selector:

All Networks
Current Network
Selected Network

Application selector:

All Applications
Active
Background
System

## Dashboard behavior

The live graph should be visually dominant but not consume the entire page.

Cards should not all have identical dimensions if doing so creates a monotonous grid. Use a deliberate composition.

Avoid placing 10 equal KPI cards in a row.

---

# 23. PAGE SPECIFICATION: TRAFFIC

Purpose:
Deep inspection of traffic over time.

Main components:

- large interactive timeline chart
- time-range selector
- aggregation selector
- download/upload/total toggle
- network filter
- application filter
- foreground/background filter
- exact statistic summary
- data table below chart

Chart interactions:

Hover:
show timestamp and exact values.

Click:
lock tooltip.

Drag:
zoom selection.

Double click:
reset zoom.

Keyboard:
allow accessible focus and data table alternative.

Do not require the chart to communicate exact values without an accessible table.

---

# 24. PAGE SPECIFICATION: APPLICATIONS

Purpose:
Find which applications consume bandwidth.

Top section:

Period selector
Traffic mode
Active/background filter
Search

Hero visual:

Top application ranking.

Main table:

Rank
Application
Status
Download
Upload
Total
Share
Peak rate
Last active
Attribution

Clicking an application opens a detail panel/page.

Application detail:

Overview
Traffic timeline
Network breakdown
Sessions
Peak periods
History

Top 5 controls:

Metric:
Total / Download / Upload

Period:
Today / Week / Month / Year / Custom

Scope:
All / Active / Background / System

---

# 25. PAGE SPECIFICATION: NETWORKS

Purpose:
Understand where the laptop has connected and how much traffic each network consumed.

Primary list:

Network name
Connection type
Total
Download
Upload
Sessions
Last used
Average session

Network detail:

Network identity
Observed metadata
Traffic history
Usage by application
Session history
Speed history
Connection events

Use a human-readable alias as the primary display name.

Keep raw metadata accessible but not visually dominant.

Do not expose sensitive network credentials.

---

# 26. PAGE SPECIFICATION: SESSIONS

Purpose:
Understand individual connection periods.

Table:

Start
End
Duration
Network
Download
Upload
Peak
Top Application
Quality

Click opens session detail.

Session timeline should combine:

- traffic
- network changes
- peak points
- top application changes

Avoid unnecessary decorative timelines. Every marker should have meaning.

---

# 27. PAGE SPECIFICATION: SPEED & QUALITY

Purpose:
Observe actual transfer speed and connection characteristics.

Sections:

Current

Average download
Average upload
Peak download
Peak upload

Quality

Latency
Jitter
Packet loss
Stability

Historical chart:

Speed over time

Controls:

Passive observations
Active probes, when enabled

Make the source of a metric explicit.

Example:

"Passive transfer rate"
versus
"Active ping probe"

---

# 28. PAGE SPECIFICATION: HISTORY

Purpose:
Explore long-term data.

Primary visual:
Calendar heatmap.

Calendar cell meaning:
Total usage for that day.

Secondary:
Monthly line/bar chart.

Comparison:
Today vs yesterday
This week vs last week
This month vs last month
This year vs last year

Clicking a day opens a day detail.

Day detail:

Download
Upload
Total
Peak hour
Top app
Top network
Sessions

Do not rely on color alone in the calendar.

---

# 29. PAGE SPECIFICATION: ANALYTICS

Purpose:
Turn recorded data into useful explanations.

Cards/insights:

- usage trend
- largest application
- largest network
- peak hour
- peak day
- background usage
- hotspot usage
- change versus previous period
- unusual spikes

Every insight must display the data scope used to compute it.

Example:

"Hotspot usage increased 42% compared with the previous 7-day period."

Under it:

Period: Aug 26-Sep 1
Comparison: Aug 19-Aug 25

Avoid vague AI-like statements such as:

"Your network seems busier than usual."

Prefer measurable statements.

---

# 30. PAGE SPECIFICATION: ALERTS

Purpose:
Help users react to important events.

Default alert categories:

- high daily usage
- high hotspot usage
- network disconnect
- network reconnect
- unusual usage spike
- process using large amount of data
- connection quality degradation
- collector problem

Each alert rule needs:

Enable
Threshold
Scope
Cooldown
Notification channel

Avoid notification spam.

Use a cooldown and group similar alerts.

Use Windows App Notifications for local user-facing notifications where appropriate.

---

# 31. PAGE SPECIFICATION: DATA

Purpose:
Give the user ownership and transparency.

Sections:

Database

- current database size
- last write
- history coverage
- retention policy
- integrity check

Export

CSV
JSON
SQLite backup

Import/restore

Validate backup before replacing live data.

Privacy

Explain local-only behavior.

---

# 32. PAGE SPECIFICATION: SETTINGS

Sections:

General
Monitoring
Appearance
Notifications
Data & Retention
Privacy
Advanced
About

General:

- start with Windows
- start minimized
- tray behavior

Monitoring:

- sampling behavior
- active probing
- included/excluded interfaces
- system-process handling

Appearance:

- dark/light/system
- accent
- density
- reduced motion

Notifications:

- enable/disable
- thresholds
- sound

Data & Retention:

- retention
- backup path
- export
- cleanup

Privacy:

- local-only status
- packet/content inspection status
- optional telemetry status

Advanced:

- diagnostics
- logging level
- collector restart
- database maintenance

Do not bury critical privacy behavior in Advanced.

---

# 33. GLOBAL COMPONENT LIBRARY

Build reusable components before duplicating markup.

Core:

AppShell
TitleBar
NavigationRail
PageHeader
SectionHeader
GlassPanel
SolidPanel
MetricCard
MetricValue
TrendBadge
StatusIndicator
NetworkBadge
ApplicationBadge
TrafficPill
FilterBar
TimeRangePicker
SegmentedControl
SearchField
DataTable
VirtualizedTable
ChartContainer
Legend
Tooltip
EmptyState
ErrorState
LoadingState
OfflineState
ConfirmationDialog
CommandPalette
Toast/InfoBar
NotificationPreview
ContextMenu
DetailDrawer
PropertyList
StatGrid
Timeline
CalendarHeatmap

Chart-specific components:

TrafficLineChart
StackedTrafficChart
UsageBarChart
TopApplicationsChart
NetworkShareChart
SpeedChart
LatencyChart
CalendarUsageHeatmap

Each reusable component must specify:

- anatomy
- states
- theme tokens
- hover behavior
- focus behavior
- keyboard behavior
- disabled behavior
- loading behavior
- error behavior
- reduced-motion behavior
- minimum width

---

# 34. DATA TABLE DESIGN

Tables are important because Internet Tracer is a data-analysis tool.

Requirements:

- readable density
- sticky header where useful
- sortable columns
- resizable columns where practical
- keyboard navigation
- selected row state
- hover state
- empty state
- loading skeleton or progress indicator
- truncation with tooltip for long process names/paths
- units aligned consistently
- numeric columns right aligned when useful

Do not hide important metrics behind hover-only interactions.

Support responsive behavior at smaller window sizes:

1. Hide low-priority columns.
2. Allow horizontal scrolling for true data grids.
3. Provide details on row selection.
4. Never squeeze text until it becomes unreadable.

---

# 35. CHART DESIGN SYSTEM

Charts must answer a question.

Before adding a chart, define:

What is being compared?
What is the x-axis?
What is the y-axis?
What unit is displayed?
What time scope is active?
What does color mean?
What happens when data is missing?

Preferred chart choices:

Line/area:
Traffic over time, speed over time.

Bar:
Daily totals, top applications, top networks.

Stacked bar:
Download versus upload across categories.

Donut:
Small number of composition slices only. Avoid for large rankings.

Heatmap:
Calendar/day-hour usage patterns.

Table:
Exact data and detailed comparison.

Do not use a chart just because it looks interesting.

Avoid 3D charts.

Avoid unnecessary gradients inside chart series.

Avoid glowing lines that reduce legibility.

Always support tooltip + accessible tabular detail.

---

# 36. TOP 5 SYSTEM

The Top 5 module is a signature feature.

Modes:

TOTAL
DOWNLOAD
UPLOAD

Scopes:

ALL APPLICATIONS
ACTIVE
BACKGROUND
SYSTEM

Periods:

LIVE
TODAY
WEEK
MONTH
YEAR
CUSTOM

Top 5 ranking must update smoothly.

When the ranking changes:

- animate position changes
- preserve identity
- avoid flashing
- do not reshuffle every tiny byte delta

Use a stability threshold for visual reordering if needed.

Example:

Steam 7.54 GB
Antigravity 4.73 GB
Chrome 4.83 GB

The ranking order must be numerically correct, even if the visual animation temporarily moves rows.

---

# 37. HOTSPOT / METERED NETWORK EXPERIENCE

When a network is identified as likely mobile/tethered or marked by the user as metered, show a clear but non-intrusive indicator.

Example:

HOTSPOT
2.84 GB used today

Top consumer:
Antigravity 1.24 GB

Possible future feature:
User-defined daily or monthly hotspot budget.

Do not infer a carrier quota unless the OS or user configuration actually provides it.

---

# 38. SEARCH AND COMMAND PALETTE

Global search should allow:

- application name
- network name
- date
- session
- event

Optional command palette:

Ctrl+K

Commands:

Open Dashboard
Open Traffic
Show Hotspot Usage
Export CSV
Open Settings
Pause Monitoring
Open current network

The command palette should feel like a native productivity feature, not an AI chatbot.

No AI chat box is required for the core experience.

---

# 39. EMPTY, LOADING, ERROR, AND DEGRADED STATES

## Empty

Explain why no data exists and what the user can do next.

Example:

"No traffic history yet"

"Internet Tracer needs a few moments of monitoring before historical charts can appear."

## Loading

Use compact skeletons or progress indicators.

Do not block the entire window because a single chart is loading.

## Error

Explain:

What failed
Whether monitoring is affected
What can be done

## Degraded

Example:

"Traffic is being measured, but application attribution is partially unavailable."

This is better than showing a blank or pretending attribution is complete.

---

# 40. RESPONSIVE DESKTOP BEHAVIOR

Internet Tracer is desktop-first, but users can resize windows.

Design at three mental modes:

Wide
approximately 1440px+ content width

Compact
approximately 1024-1439px

Narrow desktop
below approximately 1024px

Do not stack everything blindly at smaller widths.

Priority rules:

1. Critical current traffic remains visible.
2. Filters remain accessible.
3. Navigation stays usable.
4. Secondary visuals can collapse.
5. Tables can switch to detail drawers.

Never allow:

- text overlap
- clipped buttons
- hidden focus indicators
- chart controls outside the viewport
- title bar collision
- unreadable table columns

---

# 41. ACCESSIBILITY

Minimum target:
WCAG 2.2 AA-inspired discipline for applicable interface behavior, plus Windows accessibility conventions.

Requirements:

- visible keyboard focus
- logical tab order
- accessible names for controls
- keyboard operation of menus/dialogs/tabs
- sufficient color contrast
- do not communicate status by color alone
- focus must not be hidden by overlays
- readable text sizes
- adequate control spacing
- reduced motion
- tooltips for ambiguous icons
- table data alternative for charts

Focus styles must be clearly visible against adjacent surfaces.

Do not disable native keyboard conventions for aesthetics.

---

# 42. PERFORMANCE TARGETS

These are product targets, not promises until measured on target hardware.

Idle monitor service:
Very low CPU usage, typically close to zero between collection work.

UI idle:
No continuous high-frequency render loop unless the live dashboard is active.

Memory:
Avoid unbounded in-memory arrays for historical data.

Live chart:
Use a bounded rolling window.

Database:
Batch writes.

Charts:
Downsample data to the screen's available resolution instead of rendering millions of points.

Long-running process list:
Virtualize large tables.

Animation:
Prefer compositor-friendly properties and avoid expensive layout-triggering animation.

Start time:
UI should become usable quickly even if historical analytics continues loading.

Collector isolation:
A chart rendering problem must not stop monitoring.

Monitoring isolation:
A collector problem must not crash the UI.

---

# 43. IPC BETWEEN COLLECTOR AND UI

Preferred pattern:

Collector/service owns acquisition.
UI owns presentation.
SQLite owns durable history.

For live updates, use a local IPC mechanism or shared in-process event stream depending on final packaging.

If IPC is used:

- local only
- authenticated/authorized
- strict message schemas
- bounded message rates
- no arbitrary command execution

Example live message:

{
  timestamp,
  interfaceId,
  networkId,
  downloadBps,
  uploadBps,
  collectorState
}

Do not send thousands of tiny UI messages per second.

Coalesce updates to a human-meaningful frame rate.

---

# 44. OBSERVABILITY OF INTERNET TRACER ITSELF

Internet Tracer should monitor its own health.

Internal diagnostics:

Collector state
Database state
Last sample timestamp
Last successful write
UI-to-service connection state
Attribution state
Dropped sample count
Queue depth
Database write latency
Migration status

A diagnostic page can expose this without cluttering the main dashboard.

---

# 45. LOGGING

Log levels:

Error
Warning
Info
Debug
Trace

Production default:
Info or Warning depending on privacy/performance requirements.

Never log:

- passwords
- Wi-Fi credentials
- raw packet payloads
- unrestricted environment secrets

Avoid logging every sample. That would destroy the lightweight requirement.

Implement rate-limited repeated-error logging.

---

# 46. INSTALLATION AND UPDATE DESIGN

Installer should clearly explain:

- background monitoring
- startup behavior
- local storage location
- permissions if any
- privacy behavior

Uninstall should explain whether historical data is retained or deleted.

Never delete historical data without explicit user confirmation.

Future update mechanism must preserve database migrations safely.

---

# 47. FILE/FOLDER STRUCTURE

Suggested repository structure:

/src
  /InternetTracer.App
  /InternetTracer.Core
  /InternetTracer.Monitor
  /InternetTracer.Data
  /InternetTracer.Analytics
  /InternetTracer.Infrastructure
/tests
  /InternetTracer.Core.Tests
  /InternetTracer.Data.Tests
  /InternetTracer.Monitor.Tests
  /InternetTracer.Analytics.Tests
  /InternetTracer.IntegrationTests
  /InternetTracer.UiTests
/docs
  /architecture
  /design
  /decisions
  /testing
  /privacy
/tools
  /fixtures

Keep generated build output out of source directories.

---

# 48. DEVELOPMENT WORKFLOW FOR THE AI AGENT

Phase 0: Repository reconnaissance

- inspect files
- identify frameworks
- identify existing code
- inspect package versions
- inspect build scripts
- inspect tests
- inspect README
- inspect current design tokens
- inspect OS/environment

Phase 1: Architecture proof

- validate Windows APIs
- validate collection counters
- validate network identity access
- validate application attribution feasibility
- validate service lifecycle
- validate SQLite performance

Phase 2: Skeleton

- create solution structure
- create service/collector
- create database
- create app shell
- create IPC if needed

Phase 3: Measurement engine

- interface counters
- sampling
- delta calculation
- network sessions
- database aggregation

Phase 4: Attribution

- process inventory
- process traffic attribution
- attribution confidence
- unknown bucket

Phase 5: UI design system

- tokens
- shell
- typography
- materials
- components
- motion

Phase 6: Core pages

Dashboard
Traffic
Applications
Networks

Phase 7: Historical pages

Sessions
Speed & Quality
History
Analytics

Phase 8: Reliability

Alerts
Data
Settings
Backup/restore

Phase 9: Performance

profiling
query optimization
memory testing
long-run testing

Phase 10: QA and release candidate

installation
startup
network switching
sleep/resume
offline
adapter changes
process lifecycle
large history
UI resizing
light/dark
reduced motion
permissions
upgrade/migration

---

# 49. DESIGN PROCESS FOR GEMINI 3.1 PRO HIGH

When asked to design a page, do not immediately code.

First produce:

1. Page purpose.
2. User questions answered by the page.
3. Information hierarchy.
4. Layout anatomy.
5. Component tree.
6. Responsive rules.
7. Interaction model.
8. Motion model.
9. Empty/loading/error states.
10. Light/dark adaptations.
11. Accessibility behavior.
12. Potential overlap/conflict analysis.
13. Implementation impact analysis.

Then implement.

After implementation:

1. Render at target window sizes.
2. Inspect the actual visual hierarchy.
3. Compare spacing.
4. Check for collisions.
5. Check chart readability.
6. Check density.
7. Check hover/focus.
8. Check light mode.
9. Check reduced motion.
10. Check that the result still looks like Internet Tracer rather than a generic generated dashboard.

Only after this review is the UI considered complete.

---

# 50. ANTI-AI-SLOP RULES

Absolutely avoid:

- generic purple/blue neon gradients used without semantic purpose
- endless rounded cards
- giant hero headings inside every desktop page
- giant empty whitespace that wastes a desktop monitor
- generic "Welcome back" copy
- stock SaaS dashboard composition
- random glassmorphism on every element
- glowing borders everywhere
- fake 3D network globes without useful information
- decorative particle backgrounds
- arbitrary orbit animations
- meaningless animated counters
- huge icons next to every label
- unnecessary radial gauges
- excessive donut charts
- excessive use of pills
- UI that looks copied from Dribbble
- default Tailwind/shadcn-looking visual style
- template-like cards with identical height and rhythm
- generic AI sparkle icons unless they represent a real system feature
- fake AI assistant/chat UI simply because it is fashionable

Internet Tracer is a network observability product. Its visual language should come from network flow, time, signal, continuity, connection state, and measured data.

The design must be distinctive without being theatrical.

---

# 51. ORIGINAL INTERNET TRACER VISUAL LANGUAGE

Use the product concept itself as the source of visual identity.

Core motifs:

- tracing
- signal movement
- connection handoff
- temporal flow
- measured lines
- subtle propagation
- layered depth
- network continuity

Examples of meaningful motion:

A new network connection subtly propagates from the network indicator into the session header.

A live traffic spike briefly changes the visual emphasis of the line chart.

A newly dominant application moves upward in Top 5 instead of teleporting.

A data point can be selected and visually traced through the corresponding table row.

These interactions make the interface feel authored.

---

# 52. MICROCOPY RULES

Prefer short factual labels.

Good:

Current network
Traffic today
Top applications
Background traffic
Unattributed

Avoid:

Your awesome network journey
Unlock your bandwidth potential
Supercharge your connection

Microcopy should sound like a professional technical product.

---

# 53. NUMBER FORMATTING

Use human-readable units:

B
KB
MB
GB
TB
PB

Use binary or decimal units consistently. Choose one system and disclose it in Settings/About.

Recommended:

Bytes: B, KiB, MiB, GiB for binary storage calculations.
Network rate: bit/s units, for example Mbps.

If the UI decides to use decimal GB for user familiarity, apply that consistently and document the convention.

Never mix MB/s and Mbps without clearly labeling them.

Use sensible precision:

< 10: 2 decimals where useful
10-999: 1 decimal where useful
>= 1000: appropriate unit conversion

Avoid pointless precision such as:

4.823984 GB

Use:

4.82 GB

---

# 54. TIME FORMATTING

Store UTC internally.

Display local time by default.

Use absolute dates for historical data:

2 Sep 2026, 14:42

For recent events:

14:42:18

Use relative time only when useful and provide exact time on hover/details.

Respect locale settings.

---

# 55. SEARCHABLE DATA EXPLORER

The data explorer should allow filtering by:

Date range
Network
Interface
Application
Process state
Direction
Attribution state

Columns:

Timestamp
Network
Application
Download
Upload
Total
Rate
Attribution

Provide export for the filtered dataset.

Do not load millions of records into the UI at once.

Use pagination, virtualization, aggregation, and lazy loading.

---

# 56. BACKUP AND RESTORE

Backup must contain:

- SQLite database
- schema version
- metadata required to restore

Before restore:

- validate file
- validate schema
- validate integrity
- show date range
- show database size
- require explicit confirmation

Provide safe restore behavior with rollback.

---

# 57. FAILURE MODES

Internet Tracer must continue operating when:

- Wi-Fi disappears
- Ethernet disappears
- adapter changes
- IP changes
- gateway changes
- the UI closes
- the system sleeps
- the system resumes
- a process starts/stops
- process attribution becomes unavailable
- SQLite experiences a transient lock
- a database migration is pending
- a network name changes
- the active network cannot be identified fully

Do not crash the whole application for one missing interface.

---

# 58. SLEEP / RESUME HANDLING

On system resume:

1. Re-enumerate interfaces.
2. Re-check counters.
3. Detect counter resets.
4. Re-detect current network.
5. Close/reopen sessions appropriately.
6. Avoid treating the sleep duration as traffic.
7. Record a system resume event if useful.

Never generate a false giant traffic spike because a counter changed unexpectedly after resume.

---

# 59. NETWORK SWITCHING EDGE CASES

Handle:

- Wi-Fi to Ethernet
- Ethernet to Wi-Fi
- Wi-Fi to hotspot
- hotspot reconnect
- duplicate SSIDs
- multiple adapters
- VPN interfaces
- virtual adapters
- loopback
- Hyper-V adapters
- Docker/WSL interfaces

Provide an interface inclusion/exclusion policy.

Do not assume every interface should be counted as "internet usage".

By default, prefer physical/primary network interfaces and clearly classify virtual adapters.

Allow advanced users to include/exclude adapters manually.

---

# 60. VPN AND VIRTUAL NETWORKS

VPNs can change routing and interface topology.

The product should distinguish:

Physical adapter traffic
Virtual/VPN adapter traffic

Avoid double-counting aggregated traffic when the same bytes are observed at multiple layers.

The final design must define one authoritative accounting layer for "total laptop internet usage" and treat other interfaces as diagnostic views.

This is a key architecture decision and must be validated experimentally.

---

# 61. CORRECTNESS MODEL

Define:

Authoritative total traffic = traffic counted at the chosen accounting interface layer.

Application traffic = attributable portion of that authoritative traffic.

Unknown traffic = authoritative traffic - confidently attributed application traffic.

The following invariant should hold within defined tolerance:

Total interface traffic >= sum(attributed applications)

The difference should be shown as Unattributed rather than hidden.

Do not create impossible totals where applications consume more than the authoritative interface total unless there is a documented multi-interface scope difference.

---

# 62. TEST PLAN

Unit tests:

- delta calculation
- counter reset handling
- aggregation
- unit conversion
- ranking
- trend comparison
- network fingerprinting
- time-zone conversion
- retention
- database migration

Integration tests:

- interface discovery
- database write/read
- service restart
- UI/service communication
- network switch
- process start/stop

Long-running tests:

- 24h simulation
- 7-day accelerated simulation
- millions of aggregate rows
- repeated network switching
- thousands of process observations

UI tests:

- navigation
- filters
- charts
- table sorting
- keyboard
- dialogs
- theme switching
- reduced motion
- resizing

Resilience tests:

- database lock
- corrupted fixture
- unavailable attribution API
- interface disappears
- unexpected counter reset
- sleep/resume
- service unavailable

---

# 63. VISUAL QA CHECKLIST

Before accepting any page, verify:

Hierarchy
- Is the most important information visible first?
- Is the page visually balanced?

Spacing
- Are margins and gaps consistent?
- Are there cramped or overly empty regions?

Alignment
- Do cards, charts, tables, and headings align to a shared grid?

Overlap
- Does any element collide with another at min/normal/max sizes?

Typography
- Are numbers easy to scan?
- Are secondary labels readable?

Charts
- Can the user understand the chart within seconds?
- Are axes and units clear?
- Is exact data accessible?

Color
- Does color have semantic meaning?
- Is contrast sufficient?

Motion
- Does animation communicate something?
- Is motion short enough?
- Does reduced-motion work?

Interaction
- Are hover/focus/pressed/disabled states present?
- Does keyboard navigation work?

Themes
- Dark mode
- Light mode

States
- loading
- empty
- error
- degraded
- offline

AI-slop test
- Does it look like a generic AI dashboard?
- Are there meaningless decorative elements?
- Could this interface plausibly belong to ten unrelated SaaS products?

If yes, redesign.

---

# 64. DESIGN REVIEW QUESTIONS BEFORE ANY UI CHANGE

Before changing any component, answer internally or in the implementation notes:

1. What user problem does this change solve?
2. Which component owns the behavior?
3. Which page layouts depend on the component?
4. What is its minimum viable width?
5. What happens in light mode?
6. What happens in dark mode?
7. What happens with reduced motion?
8. What happens with keyboard-only input?
9. What happens with long text?
10. What happens with empty data?
11. What happens with a loading state?
12. What happens when the data is partially unavailable?
13. Does it cause another component to move or overlap?
14. Does it increase rendering or database cost?
15. Does it introduce a new visual pattern that should become a token or reusable component?

If a change introduces a one-off visual rule without strong reason, reconsider it.

---

# 65. ARCHITECTURAL DECISION RECORDS TO MAINTAIN

For every major irreversible or expensive decision, create an ADR.

Required ADRs:

ADR-001 Windows UI framework
ADR-002 Collector lifecycle
ADR-003 authoritative accounting layer
ADR-004 process attribution method
ADR-005 network fingerprint strategy
ADR-006 SQLite retention strategy
ADR-007 IPC mechanism
ADR-008 charting library
ADR-009 installer/package model
ADR-010 startup strategy
ADR-011 privacy boundary
ADR-012 VPN/virtual adapter accounting

An ADR must contain:

Context
Decision
Alternatives
Reason
Trade-offs
Validation
Date

---

# 66. ACCEPTANCE CRITERIA FOR THE FIRST FUNCTIONAL RELEASE

Internet Tracer is not considered functional merely because a dashboard renders.

Minimum acceptance:

1. App launches reliably.
2. Collector starts and remains alive independently of UI.
3. Current network is identified when possible.
4. Download/upload totals change correctly as traffic occurs.
5. Counters survive normal application restarts.
6. History is stored locally.
7. Network switching is recorded.
8. Top 5 ranking is numerically correct.
9. Unattributed traffic is preserved.
10. UI remains responsive while collection runs.
11. Database growth is bounded by retention rules.
12. Theme switching works.
13. Keyboard navigation works for core controls.
14. Reduced motion works.
15. Export works.
16. Backup/restore has validation.
17. Service recovery works.
18. Sleep/resume does not create false traffic spikes.
19. No production feature requires cloud connectivity.
20. No major layout overlap exists at supported window sizes.

---

# 67. DEFINITION OF DONE FOR A PAGE

A page is done only when:

- functional behavior is implemented
- data is correct
- loading/empty/error/degraded states exist
- dark mode validated
- light mode validated
- responsive desktop sizes validated
- keyboard/focus validated
- reduced motion validated
- performance checked
- no overlap/clipping
- charts have exact-data access where applicable
- visual hierarchy matches the design system
- no new inconsistent components were introduced
- automated tests exist for important behaviors
- a human visual review has been performed

---

# 68. DEFINITION OF DONE FOR THE WHOLE PRODUCT

The product is release-candidate quality when:

- measurement is trustworthy
- attribution is honest
- network identity is stable enough for long-term history
- background operation is reliable
- history can survive years through aggregation/retention
- UI feels cohesive across all pages
- light/dark are equally intentional
- animations are purposeful
- the application feels fast
- privacy claims are technically true
- exports/backups work
- migrations are safe
- installer behavior is correct
- sleep/resume/network switching have been tested
- visual QA finds no obvious AI-slop patterns

---

# 69. IMPORTANT TECHNICAL REALITY CHECKS

The following must be validated before promising them as exact capabilities:

1. Per-process upload/download accounting.
2. Exact relationship between physical interface totals and virtual/VPN adapters.
3. Reliable BSSID availability on all supported Windows states.
4. Foreground/background classification semantics.
5. Permission requirements for low-level collection.
6. Service/UI communication under user-session changes.
7. Startup behavior under Windows security policies.
8. Notification behavior when running elevated.
9. Active latency/jitter/packet-loss measurement strategy.
10. Long-term database performance.

When a capability has a platform limitation, expose the limitation in the product rather than faking precision.

---

# 70. FINAL IMPLEMENTATION PHILOSOPHY

Build Internet Tracer in layers.

Measure first.

Store second.

Validate third.

Analyze fourth.

Design fifth.

Polish sixth.

Do not hide correctness problems behind beautiful UI.

Do not hide poor UI behind correct backend code.

Do not sacrifice long-run performance for a visually impressive demo.

Do not sacrifice visual identity for convenience.

Do not add features simply because an AI can generate them.

Every feature must answer a real user question.

Every visualization must represent real data.

Every animation must have purpose.

Every metric must have a definition.

Every privacy claim must match the implementation.

Every substantial UI change must be checked for cascading layout effects.

Internet Tracer should feel like a carefully engineered product that happens to have a beautiful interface, not a beautiful interface wrapped around a prototype.

---

# 71. WEB AND DESIGN REFERENCE BASIS

Use these as principle references, not as templates to copy.

Microsoft Fluent 2 Windows design and materials:
https://fluent2.microsoft.design/components/windows
https://fluent2.microsoft.design/material

Microsoft WinUI 3 desktop app structure, Mica, title bar, NavigationView:
https://learn.microsoft.com/en-us/windows/apps/develop/ui/windows-app-sdk-app-structure
https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/navigationview
https://learn.microsoft.com/en-us/windows/apps/design/controls/title-bar

Microsoft networking APIs:
https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkinterface.getipv4statistics
https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlangetnetworkbsslist
https://learn.microsoft.com/en-us/windows/win32/etw/tcpip

Microsoft startup/service guidance:
https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_delayed_auto_start_info

Microsoft Windows app notifications:
https://learn.microsoft.com/en-us/windows/apps/develop/notifications/

W3C accessibility:
https://www.w3.org/TR/WCAG22/
https://www.w3.org/WAI/standards-guidelines/wcag/new-in-22/

Radix accessibility and interaction patterns:
https://www.radix-ui.com/primitives/docs/overview/accessibility
https://www.radix-ui.com/primitives/docs/overview/introduction

Atlassian motion guidance:
https://atlassian.design/foundations/motion
https://atlassian.design/foundations/motion/applying-motion

Apple material and motion guidance:
https://developer.apple.com/design/human-interface-guidelines/materials
https://developer.apple.com/design/human-interface-guidelines/motion

Data visualization should follow established principles from professional design systems and accessibility practice. Do not copy any visual layout from reference examples.

---

# 72. FIRST TASK FOR THE AGENT

Before coding:

1. Read this entire document.
2. Inspect the repository.
3. Report the current stack and repository state.
4. Identify conflicts between this specification and the existing codebase.
5. Build a capability matrix for:
   - interface counters
   - network identity
   - process attribution
   - network switching
   - active quality measurement
   - startup/service
   - SQLite
   - UI framework
6. Identify assumptions that require proof-of-concept validation.
7. Produce a phased implementation plan with dependencies.
8. Propose ADRs for the high-risk decisions.
9. Do not start mass-generating UI code until the design system and shell architecture are agreed internally and validated against the repository.

When implementation starts, work in small verifiable increments.

After each increment:

- build
- test
- inspect
- record what changed
- check regression risk

Never treat the end of a coding pass as the end of engineering work.

---

# 73. SUCCESS IMAGE IN ONE SENTENCE

When the user opens Internet Tracer, it should immediately feel like a premium Windows network observability tool with a distinct identity, precise information hierarchy, fluid but restrained motion, glass/material depth used intentionally, excellent dark and light modes, and trustworthy local data that lets the user understand exactly where the laptop's internet usage is going.
