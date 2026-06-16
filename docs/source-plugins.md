# CLI 源插件系统

CodeIsland Runtime 支持通过 JSON 插件文件扩展支持的 CLI 源，无需重新编译。

## 快速开始

### 1. 创建插件文件

在 `%AppData%\CodeIsland\sources\` 目录下创建 JSON 文件（例如 `my-cli.json`）：

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

### 2. 配置 CLI 工具

配置你的 CLI 工具调用 CodeIsland Bridge 时指定 `--source` 参数：

```bash
CodeIsland.Bridge.exe --source my-cli
```

### 3. 启动 Runtime

启动 CodeIsland Runtime，插件将自动加载。你的 CLI 源将出现在 `/api/sources` 端点中。

---

## 插件格式参考

### 必需字段

#### `schema_version` (string)
插件模式版本。当前必须为 `"1.0"`。

#### `source` (object)
源的元数据：

- **`key`** (string, 必需)  
  唯一标识符，2-64 字符，只能包含小写字母、数字和连字符，必须以字母或数字开头和结尾。
  
  示例：`"my-cli"`, `"custom-agent-v2"`
  
  ⚠️ **不能与内置源冲突**（claude, codex, cursor 等）

- **`display_name`** (string, 必需)  
  显示名称，1-100 字符。
  
  示例：`"My Custom CLI"`, `"企业 AI 助手"`

- **`icon_name`** (string, 必需)  
  图标标识符，1-64 字符。
  
  示例：`"terminal"`, `"robot"`, `"mycli"`

- **`permission_response_style`** (enum, 必需)  
  权限响应格式。可选值：
  - `"claude-style"`: Claude Code 风格（推荐）
  - `"codex"`: Codex CLI 风格

### 可选字段

#### `event_mappings` (object)
将 CLI 特定的事件名称映射到标准事件名称。

**标准事件名称**：
- `PreToolUse` - 工具执行前
- `PostToolUse` - 工具执行后
- `UserPromptSubmit` - 用户提交提示词
- `SessionStart` - 会话开始
- `SessionEnd` - 会话结束
- `Stop` - 停止
- `SubagentStart` - 子代理启动
- `SubagentStop` - 子代理停止
- `Notification` - 通知
- `PermissionRequest` - 权限请求
- `PostToolUseFailure` - 工具执行失败
- `PreCompact` - 压缩前

**示例**：
```json
"event_mappings": {
  "beforeToolExec": "PreToolUse",
  "afterToolExec": "PostToolUse",
  "sessionInit": "SessionStart",
  "sessionEnd": "Stop"
}
```

---

## 完整示例

### 最小插件
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

### 完整插件（带事件映射）
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

## 验证规则

### `source.key` 验证
- 长度：2-64 字符
- 格式：`^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$`
- ✅ 有效：`my-cli`, `agent-v2`, `tool123`
- ❌ 无效：`My-CLI`（大写）, `-cli`（开头连字符）, `a`（太短）

### 冲突检测
插件不能覆盖内置源。如果 `source.key` 与内置源冲突，插件将被跳过并记录警告。

**内置源列表**（部分）：
- claude, codex, cursor, gemini, trae, copilot, cline, qoder, kimi, pi, kiro 等

### 事件映射验证
映射值必须是有效的标准事件名称（见上方列表）。无效的映射将导致插件加载失败。

---

## 故障排除

### 插件未出现在源列表中

**原因 1：JSON 格式错误**
```
[SourcePlugin] Error: Plugin 'my-cli.json': Invalid JSON: ...
```
**解决**：使用 JSON 验证工具检查语法。

**原因 2：缺少必需字段**
```
[SourcePlugin] Error: Plugin 'my-cli.json': Missing required 'source.key'
```
**解决**：确保所有必需字段都存在。

**原因 3：`source.key` 格式无效**
```
[SourcePlugin] Error: Plugin 'my-cli.json': 'source.key' must match pattern: ...
```
**解决**：使用小写字母、数字和连字符，长度 2-64 字符。

**原因 4：与内置源冲突**
```
[SourcePlugin] Warning: Plugin 'claude' conflicts with built-in source (skipped)
```
**解决**：使用不同的 `source.key`。

### 如何查看日志

Runtime 将插件加载错误输出到标准错误流（stderr）。

在控制台运行 Runtime 时，错误会显示为：
```
[SourcePlugin] Error: Plugin 'bad-plugin.json': Invalid JSON: ...
```

---

## 限制（Phase 1）

### ✅ 支持的功能
- 定义源元数据（名称、图标、key）
- 事件名称映射
- 选择权限响应格式

### ❌ 暂不支持
- **自动 Hook 安装**：插件不会自动配置 CLI hooks，需要手动设置
- **热重载**：插件在 Runtime 启动时加载，修改后需要重启
- **插件市场**：无下载或发现机制
- **数字签名**：无验证或信任系统
- **项目级插件**：只支持全局 `%AppData%` 位置

### 未来计划
- Phase 2: 基于模板的 Hook 自动安装
- Phase 3: 热重载和插件管理 API
- Phase 4: 插件市场和发现机制

---

## 插件文件位置

**Windows**:  
```
%AppData%\CodeIsland\sources\
C:\Users\<用户名>\AppData\Roaming\CodeIsland\sources\
```

**Linux/macOS** (未来支持):  
```
~/.config/CodeIsland/sources/
```

Runtime 会在首次访问源注册表时自动创建此目录。

---

## 技术细节

### 加载时机
插件在首次访问 `CodeIslandSourceAdapterRegistry` 时懒加载（通常是第一个 hook 事件到达时）。

### 错误隔离
单个插件的加载错误不会影响其他插件或 Runtime 稳定性。无效插件会被跳过并记录错误。

### 内置源保护
插件无法覆盖内置源。如果 `source.key` 冲突，内置源优先，插件被跳过。

### 优先级
加载顺序：
1. 内置源（优先级最高）
2. 插件（按文件名字母顺序）
3. 如果多个插件使用相同的 key，第一个加载的插件生效

---

## 参考资料

- [API 参考](api-reference.zh.md)
- [集成指南](integration-guide.zh.md)
- [Runtime/Display 契约](runtime-display-contract.md)
