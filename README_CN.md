# CodeIsland Runtime

[English](README.md)

CodeIsland Runtime 是 CodeIsland 的中心化基座。它负责接入本机 CLI hook 事件，归一化会话、审批和问答状态，并通过带 token 认证的 REST/WebSocket 接口把同一份 Runtime 状态提供给多个展示端。

这个仓库负责：

- `CodeIsland.Contracts`：公开 REST/WebSocket DTO 合同。
- `CodeIsland.Core`：hook 模型、source adapter、响应构造、transcript 读取、settings、IPC 协议。
- `CodeIsland.Hub`：Runtime 状态、HookServer、source service、REST API、WebSocket 广播、本地 token 存储。
- `CodeIsland.RuntimeHost`：独立 Runtime 进程。
- `CodeIsland.Bridge`：短生命周期 CLI hook bridge。
- Runtime 侧测试、文档和外部展示端示例。

Windows HUD 只是官方展示客户端。它应该通过 `CodeIsland.Contracts` 和 RuntimeHost/Bridge 可执行产物集成 Runtime，不应该继续编译依赖 Runtime 内部实现。

## 拓扑

默认本地 managed 模式：

```text
Windows HUD -> 启动 127.0.0.1 上的 CodeIsland.RuntimeHost -> REST/WebSocket
CLI hook -> CodeIsland.Bridge -> Runtime named pipe -> Runtime 状态
```

共享远程模式必须显式开启。只有当用户明确希望手机、Web、硬件屏幕或其他设备通过局域网连接时，才使用 `--host 0.0.0.0` 或 `api_bind_host=0.0.0.0`。默认不要开放局域网监听。

## 构建

```powershell
dotnet build CodeIsland.Runtime.slnx
dotnet test CodeIsland.Runtime.slnx
```

开发时启动 Runtime：

```powershell
dotnet run --project src/CodeIsland.RuntimeHost -- --token dev-token --port 32145 --no-repair
```

展示端连接 `http://127.0.0.1:32145`，token 使用 `dev-token`。

## 接口和展示端开发

- 完整 API 文档：`docs/api-reference.md`
- 其他应用集成方式：`docs/integration-guide.md`
- Runtime/展示端职责边界：`docs/runtime-display-contract.md`
- 展示端快速开始：`docs/external-display-client.md`

最小控制台展示端位于 `samples/external-display-console`：

```powershell
dotnet run --project samples/external-display-console -- --help
```

## Runtime 发布产物

生成包含 `CodeIsland.RuntimeHost.exe`、`CodeIsland.Bridge.exe` 和 `runtime-manifest.json` 的 Runtime ZIP：

```powershell
.\scripts\publish-runtime.ps1 -Runtime win-x64
```

Windows HUD 可以读取 Runtime 更新 manifest，下载 ZIP，并把 payload 提升到本机 Runtime 缓存目录。

## 前端集成建议

前端展示端只负责 UI、交互、动画、主题和设备适配。它应该：

- 启动时读取 Runtime `/api/health`、`/api/capabilities`、`/api/sessions`、`/api/pending`。
- 通过 `WS /api/events` 订阅变化，断线重连后重新拉取 REST 快照。
- 对审批、拒绝、问答、关闭等用户操作调用 REST action endpoint。
- 保持 UI-only 状态在本地，例如选中项、窗口位置、主题、动画、声音。
- 不读取 Hub/Core/Bridge 内部类型，不直接读 transcript 文件，不自己实现 hook response。

官方 Windows HUD 的默认体验是：启动 HUD 时启动本地 Runtime，退出 HUD 时只关闭自己拥有的本地私有 Runtime；如果 Runtime 显式绑定到 `0.0.0.0` 进入共享远程模式，HUD 退出时不关闭 Runtime，避免断开手机或其他展示端。

