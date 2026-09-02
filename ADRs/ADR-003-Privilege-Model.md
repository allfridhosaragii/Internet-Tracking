# ADR-003: Privilege Model

## Status
Accepted

## Context
Internet Tracer consists of two main active components:
1. The Background Collector (Service)
2. The UI Shell

The Collector performs SQLite persistence, passive network interface polling, and ETW Kernel Network tracing.
ETW Kernel Tracing (`Microsoft.Diagnostics.Tracing.TraceEvent` hooking `KernelTraceEventParser.Keywords.NetworkTCPIP`) strictly requires elevated privileges (`Administrator` or `LocalSystem` / `NetworkService` with explicit ETW rights).

## Decision
- **InternetTracer.Service**: Will run as a Windows Service under `LocalSystem`. This grants the necessary ETW rights natively without prompting the user continuously for UAC. It is strictly scoped to collecting data and writing to the local SQLite DB.
- **InternetTracer.UI**: Will run as the **Standard User** (Interactive User). It will *never* request UAC elevation.
- **SQLite Persistence**: Performed entirely by the Service. The UI will *not* read the SQLite database directly. This prevents file locking issues and avoids needing to grant the Standard User read-access to secure service directories (e.g., `ProgramData/InternetTracer`).

## Consequences
- Requires a robust IPC (Named Pipes) boundary between the UI and Service.
- UI cannot function if the Service is stopped.
- Installation requires Admin rights (standard for system-level networking tools).
- The principle of least privilege is maintained for the UI, limiting blast radius if the UI is compromised.
