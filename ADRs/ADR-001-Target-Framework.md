# ADR-001: Target Framework

## Status
Accepted

## Context
The Master Specification mandates .NET 10 LTS for longevity. However, at the time of development, an environment verification via `dotnet --list-sdks` confirmed that only .NET 8.0.411 and .NET 9.0.317 are installed on the build machine. .NET 10 LTS is currently unavailable in this environment.

## Decision
We will temporarily target `.net9.0` for all core, data, and monitor assemblies, and plan to migrate to `.net10.0-windows10.0.19041.0` (for WinUI 3 compatibility) as soon as the SDK becomes available.

## Consequences
- **Positive**: Development is unblocked and can proceed immediately on modern C# 13 features using .NET 9.
- **Negative**: A future commit will be required to bump the `<TargetFramework>` nodes in all `.csproj` files.
- **Compatibility**: There are no known breaking API changes expected between .NET 9 and 10 that affect our basic `System.Net.NetworkInformation` usage or SQLite bindings. We are deliberately avoiding unstable preview APIs to ensure the upgrade remains a trivial XML edit.
