# ADR-006: ETW Attribution

## Status
Accepted

## Context
Process-level network attribution relies on the `Microsoft.Diagnostics.Tracing.TraceEvent` library hooking the `Kernel Network` provider (`NetworkTCPIP`).

## Decision
- We will rely on ETW for attributing TCP and UDP traffic to specific Process IDs (PIDs).
- A load-testing prototype verified that ETW successfully captures the vast majority of traffic.
- **Coverage Limitations**:
  - Extremely short-lived processes might exit before their PID can be resolved to a human-readable name.
  - Non-TCP/UDP traffic (e.g. raw ICMP pings, ARP) may not be attributed depending on provider keywords.
  - Virtual adapters (VPNs, WSL virtual switches) generate complex event streams that might cause duplicate accounting if not carefully filtered.

## Consequences
- The Service must run as `LocalSystem` to enable the Kernel Provider.
- We must maintain an explicit `Unattributed` bucket in our analytics. We cannot assume that `Sum(Process_Traffic) == Total_Interface_Traffic`. The UI must faithfully represent the difference.
