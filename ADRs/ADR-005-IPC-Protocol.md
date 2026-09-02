# ADR-005: IPC Protocol

## Status
Accepted

## Context
The UI must communicate with the Background Service to retrieve telemetry without directly accessing SQLite or Windows APIs. The communication must be local-only, fast, and secure against arbitrary local attackers.

## Decision
- **Protocol**: Request/Response JSON framing over Local Named Pipes.
- **Framing**: Each JSON payload is sent as a newline-terminated string over the pipe stream (`StreamReader.ReadLineAsync` and `StreamWriter.WriteLineAsync`).
- **Security**: The Pipe is created with `NamedPipeServerStreamAcl` applying explicit `PipeSecurity`. `Interactive User` and `Builtin Administrators` have ReadWrite access. `Network User` and `Anonymous` are explicitly denied `FullControl`.
- **Payload Structure**:
  - `IpcRequest`: `RequestId`, `Version`, `Operation`, `Payload`
  - `IpcResponse`: `RequestId`, `Version`, `StatusCode`, `ErrorCode`, `Payload`

## Consequences
- Requires a dedicated thread or async loop in the Service to manage concurrent pipe clients.
- The UI gracefully degrades if the server pipe is unavailable (e.g. Service stopped).
- Decouples the UI completely from the underlying storage technology.
