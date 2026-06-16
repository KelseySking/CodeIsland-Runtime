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

## Extensibility

CodeIsland Runtime supports custom CLI sources through a **plugin system**. This allows you to add support for new AI CLI tools without recompiling the Runtime.

### Plugin System Features

- **Automatic CLI Detection**: Plugins can define process names, environment variables, and path patterns to automatically detect which CLI is running
- **Hook Installation**: Plugins specify how to install hooks into the CLI's configuration files
- **Bundled Plugins**: Runtime ships with built-in support for Claude Code, Codex, and more
- **User Plugins**: Drop JSON files into `%AppData%\CodeIsland\sources\` to register custom CLIs

### Quick Start

Create a plugin file (e.g., `my-cli.json`) in `%AppData%\CodeIsland\sources\`:

```json
{
  "schema_version": "2.0",
  "source": {
    "key": "my-cli",
    "display_name": "My CLI",
    "icon_name": "terminal",
    "permission_response_style": "claude-style"
  },
  "detection": {
    "process_names": ["my-cli"],
    "priority": 100
  },
  "hook_installation": {
    "format": "flat",
    "config_path": "~/.my-cli/hooks.json",
    "events": ["PreToolUse", "PostToolUse"],
    "timeout_seconds": 10
  }
}
```

Then use ConfigInstaller to install hooks:

```csharp
using CodeIsland.Core.Services;

bool success = ConfigInstaller.InstallPlugin("my-cli");
```

### Documentation

- **English**: [Plugin System Guide](docs/source-plugins.en.md) | [Plugin Schema Reference](docs/plugin-schema.en.md)
- **中文**: [插件系统指南](docs/source-plugins.md) | [插件 Schema 参考](docs/plugin-schema.md)

### Bundled Plugins

Runtime ships with these built-in CLI plugins:

- **Claude Code** (`claude.json`) - 12 events, claude-matcher format
- **Codex CLI** (`codex.json`) - 7 events, nested format with config.toml support

More bundled plugins coming soon.

## API And Display Clients

- [Documentation index](docs/README.md)
- [Full API reference](docs/api-reference.en.md)
- [Integration patterns for other apps](docs/integration-guide.en.md)
- [Runtime/display ownership contract](docs/runtime-display-contract.md)
- [Display client quickstart](docs/external-display-client.en.md)

Chinese docs:

- [中文文档索引](docs/README_CN.md)
- [完整 API 文档](docs/api-reference.md)
- [其他应用集成方式](docs/integration-guide.md)
- [Runtime/展示端职责边界](docs/runtime-display-contract.zh-CN.md)
- [展示端快速开始](docs/external-display-client.md)

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
