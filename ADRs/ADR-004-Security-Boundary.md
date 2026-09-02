# ADR-004: Security Boundary & IPC

## Status
Accepted

## Context
Because the UI runs as a Standard User and the Service runs as `LocalSystem`, an IPC mechanism is required. This mechanism must be secure to prevent local privilege escalation (LPE) or malicious local apps from snooping on the user's network traffic history or process metadata.

## Decision
- **IPC Protocol**: Local Named Pipes (`NetNamedPipeBinding` or raw `NamedPipeServerStream`).
- **ACL (Access Control List)**: The Named Pipe created by `InternetTracer.Service` will be secured with an explicit ACL. It will allow `Read/Write` access *only* to the currently logged-in Interactive User and Administrators. Guest accounts or unauthenticated network users will be explicitly denied.
- **Data Exposure**: The IPC layer will act as a strict API contract (e.g., `GetDashboardSummary`, `GetTrafficTimeline`). It will *never* expose arbitrary SQL execution to the client.
- **Cloud Telemetry**: Strictly prohibited by Master Specification.

## Consequences
- Protects process metadata and network history from unauthorized local users.
- Enforces strict API contracts, decoupling the UI from the DB schema.
- Requires robust error handling in the UI if the IPC pipe is broken, disconnected, or if the service crashes.
