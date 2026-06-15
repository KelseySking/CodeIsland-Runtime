# CodeIsland Runtime

[简体中文](README_CN.md)

CodeIsland Runtime is the centralized base process for CodeIsland display clients. It ingests CLI hook events, normalizes sessions and pending approvals/questions, and exposes one token-authenticated REST/WebSocket contract to multiple displays.

This repository owns:

- `CodeIsland.Contracts`: public REST/WebSocket DTOs.
- `CodeIsland.Core`: hook models, source adapters, response builders, transcript readers, settings, and IPC protocol.
- `CodeIsland.Hub`: Runtime state, hook server, source service, REST API, WebSocket fan-out, and local token store.
- `CodeIsland.RuntimeHost`: standalone Runtime process.
- `CodeIsland.Bridge`: short-lived CLI hook bridge.
- Runtime-facing tests, docs, and external display samples.

The Windows HUD is a display client. It should consume this Runtime through `CodeIsland.Contracts` plus RuntimeHost/Bridge executable artifacts, not by compiling against Runtime internals.

## Topology

Default local managed mode:

```text
Windows HUD -> starts CodeIsland.RuntimeHost on 127.0.0.1 -> REST/WebSocket
CLI hook -> CodeIsland.Bridge -> Runtime named pipe -> Runtime state
```

Shared remote mode is explicit. Bind Runtime with `--host 0.0.0.0` or `api_bind_host=0.0.0.0` only when the user intentionally wants LAN/mobile clients to connect with the API token.

## Build

```powershell
dotnet build CodeIsland.Runtime.slnx
dotnet test CodeIsland.Runtime.slnx
```

Run a development Runtime:

```powershell
dotnet run --project src/CodeIsland.RuntimeHost -- --token dev-token --port 32145 --no-repair
```

Then connect a display client to `http://127.0.0.1:32145` with token `dev-token`.

## API And Display Clients

- Full API reference: `docs/api-reference.md`
- Integration patterns for other apps: `docs/integration-guide.md`
- Runtime/display ownership contract: `docs/runtime-display-contract.md`
- Display client quickstart: `docs/external-display-client.md`

A minimal console display is available in `samples/external-display-console`:

```powershell
dotnet run --project samples/external-display-console -- --help
```

## Runtime Release Artifacts

Create a Runtime ZIP with `CodeIsland.RuntimeHost.exe`, `CodeIsland.Bridge.exe`, and `runtime-manifest.json`:

```powershell
.\scripts\publish-runtime.ps1 -Runtime win-x64
```

The Windows HUD can download Runtime update manifests and promote the ZIP payload into its local Runtime cache.

