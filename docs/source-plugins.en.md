# CLI Source Plugin System

CodeIsland Runtime supports extending CLI sources through JSON plugin files without recompilation.

## Quick Start

### 1. Create Plugin File

Create a JSON file (e.g., `my-cli.json`) in `%AppData%\CodeIsland\sources\`:

```json
{
  "schema_version": "1.0",
  "source": {
    "key": "my-cli",
    "display_name": "My CLI",
    "icon_name": "terminal",
    "permission_response_style": "claude-style"
  },
  "event_mappings": {
    "beforeAction": "PreToolUse",
    "afterAction": "PostToolUse"
  }
}
```

### 2. Configure CLI Tool

Configure your CLI tool to call CodeIsland Bridge with `--source` parameter:

```bash
CodeIsland.Bridge.exe --source my-cli
```

### 3. Start Runtime

Start CodeIsland Runtime, and plugins will load automatically. Your CLI source will appear in `/api/sources` endpoint.

---

## Plugin Format Reference

### Required Fields

#### `schema_version` (string)
Plugin schema version. Must be `"1.0"` currently.

#### `source` (object)
Source metadata:

- **`key`** (string, required)  
  Unique identifier, 2-64 characters, lowercase alphanumeric with hyphens, must start and end with alphanumeric.
  
  Examples: `"my-cli"`, `"custom-agent-v2"`
  
  ⚠️ **Cannot conflict with built-in sources** (claude, codex, cursor, etc.)

- **`display_name`** (string, required)  
  Display name, 1-100 characters.
  
  Examples: `"My Custom CLI"`, `"Enterprise AI Assistant"`

- **`icon_name`** (string, required)  
  Icon identifier, 1-64 characters.
  
  Examples: `"terminal"`, `"robot"`, `"mycli"`

- **`permission_response_style`** (enum, required)  
  Permission response format. Options:
  - `"claude-style"`: Claude Code style (recommended)
  - `"codex"`: Codex CLI style

### Optional Fields

#### `event_mappings` (object)
Map CLI-specific event names to standard event names.

**Standard Event Names**:
- `PreToolUse` - Before tool execution
- `PostToolUse` - After tool execution
- `UserPromptSubmit` - User submits prompt
- `SessionStart` - Session starts
- `SessionEnd` - Session ends
- `Stop` - Stop
- `SubagentStart` - Subagent starts
- `SubagentStop` - Subagent stops
- `Notification` - Notification
- `PermissionRequest` - Permission request
- `PostToolUseFailure` - Tool execution failed
- `PreCompact` - Before compaction

**Example**:
```json
"event_mappings": {
  "beforeToolExec": "PreToolUse",
  "afterToolExec": "PostToolUse",
  "sessionInit": "SessionStart",
  "sessionEnd": "Stop"
}
```

---

## Complete Examples

### Minimal Plugin
```json
{
  "schema_version": "1.0",
  "source": {
    "key": "simple-cli",
    "display_name": "Simple CLI",
    "icon_name": "terminal",
    "permission_response_style": "claude-style"
  }
}
```

### Full Plugin (with Event Mappings)
```json
{
  "schema_version": "1.0",
  "source": {
    "key": "advanced-agent",
    "display_name": "Advanced AI Agent",
    "icon_name": "robot",
    "permission_response_style": "codex"
  },
  "event_mappings": {
    "before_tool": "PreToolUse",
    "after_tool": "PostToolUse",
    "session_start": "SessionStart",
    "session_stop": "Stop",
    "agent_spawn": "SubagentStart",
    "agent_exit": "SubagentStop"
  }
}
```

---

## Validation Rules

### `source.key` Validation
- Length: 2-64 characters
- Pattern: `^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$`
- ✅ Valid: `my-cli`, `agent-v2`, `tool123`
- ❌ Invalid: `My-CLI` (uppercase), `-cli` (leading hyphen), `a` (too short)

### Conflict Detection
Plugins cannot override built-in sources. If `source.key` conflicts with a built-in, the plugin is skipped with a warning.

**Built-in Sources** (partial list):
- claude, codex, cursor, gemini, trae, copilot, cline, qoder, kimi, pi, kiro, etc.

### Event Mapping Validation
Mapping values must be valid standard event names (see list above). Invalid mappings cause plugin load failure.

---

## Troubleshooting

### Plugin Not in Source List

**Reason 1: Invalid JSON**
```
[SourcePlugin] Error: Plugin 'my-cli.json': Invalid JSON: ...
```
**Solution**: Check syntax with a JSON validator.

**Reason 2: Missing Required Field**
```
[SourcePlugin] Error: Plugin 'my-cli.json': Missing required 'source.key'
```
**Solution**: Ensure all required fields are present.

**Reason 3: Invalid `source.key` Format**
```
[SourcePlugin] Error: Plugin 'my-cli.json': 'source.key' must match pattern: ...
```
**Solution**: Use lowercase letters, digits, hyphens, 2-64 chars.

**Reason 4: Built-in Conflict**
```
[SourcePlugin] Warning: Plugin 'claude' conflicts with built-in source (skipped)
```
**Solution**: Use a different `source.key`.

### View Logs

Runtime outputs plugin loading errors to stderr.

When running Runtime in console, errors appear as:
```
[SourcePlugin] Error: Plugin 'bad-plugin.json': Invalid JSON: ...
```

---

## Limitations (Phase 1)

### ✅ Supported Features
- Define source metadata (name, icon, key)
- Event name mapping
- Select permission response format

### ❌ Not Yet Supported
- **Automatic Hook Installation**: Plugins don't configure CLI hooks automatically; manual setup required
- **Hot Reload**: Plugins loaded at startup; restart required after changes
- **Plugin Marketplace**: No discovery or download mechanism
- **Digital Signatures**: No verification or trust system
- **Per-Project Plugins**: Only global `%AppData%` location supported

### Future Phases
- Phase 2: Template-based automatic hook installation
- Phase 3: Hot reload and plugin management API
- Phase 4: Plugin marketplace and discovery

---

## Plugin File Location

**Windows**:  
```
%AppData%\CodeIsland\sources\
C:\Users\<username>\AppData\Roaming\CodeIsland\sources\
```

**Linux/macOS** (future):  
```
~/.config/CodeIsland/sources/
```

Runtime auto-creates this directory on first registry access.

---

## Technical Details

### Loading Timing
Plugins are lazy-loaded on first access to `CodeIslandSourceAdapterRegistry` (typically when first hook event arrives).

### Error Isolation
Individual plugin loading errors don't affect other plugins or Runtime stability. Invalid plugins are skipped and logged.

### Built-in Protection
Plugins cannot override built-in sources. If `source.key` conflicts, built-in takes precedence and plugin is skipped.

### Priority
Loading order:
1. Built-in sources (highest priority)
2. Plugins (alphabetical by filename)
3. If multiple plugins use same key, first loaded wins

---

## References

- [API Reference](api-reference.en.md)
- [Integration Guide](integration-guide.en.md)
- [Runtime/Display Contract](runtime-display-contract.md)
