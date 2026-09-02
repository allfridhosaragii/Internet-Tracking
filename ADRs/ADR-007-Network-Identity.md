# ADR-007: Network Identity

## Status
Accepted

## Context
The system needs to track telemetry not just per-interface, but per-network (e.g., "Home Wi-Fi" vs "Starbucks"). A physical interface (like a laptop Wi-Fi adapter) roams across many networks.

## Decision
We implemented a hierarchical fallback algorithm for `NetworkIdentity` hashing:
1. **Wi-Fi**: `Hash(GatewayIP + BSSID + SSID)`. Highest confidence. Survives DHCP changes within the same AP.
2. **Ethernet**: `Hash(NetworkInterfaceType + GatewayIP)`. Medium confidence. Will incorrectly group distinct networks if they happen to use the exact same Gateway IP (e.g. `192.168.1.1`).
3. **Fallback**: `Hash("fallback" + InterfaceGuid)`. Lowest confidence. Used for VPNs or captive portals where gateway routing is obscured.

## Consequences
- `NetworkFingerprintGenerator` cleanly separates the physical hardware identity from the logical network session.
- Users swapping between wired and wireless on the same router might still generate two distinct Network Identities depending on how the router exposes the Gateway MAC/IP, which is acceptable.
