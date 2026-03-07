# Services：CLI 与 UI 共用逻辑，所有写操作落库

本目录为 **CLI**（AI 唯一入口）与 **唯一 UIController**（页面/API 入口）共同调用的逻辑，统一以 `*Service.cs` 命名。

**架构约定**：
- **所有对世界的写操作**经 Service 完成并**落库**（Games、GameMoves、Players、叙事等）。
- **UI 需要数据时**：UIController 调 Service，Service 从持久化（DB）或与 DB 同步的内存状态读取并返回。
- **AI 操作**：只能通过 CLI；CLI 请求 `/api/UI/*`，UIController 调 Service，Service 落库（若将来「世界」拆成独立进程，可改为 Service 经 RPC 通知该进程）。
- 不在 Controller 或 CLI 中持有一份“世界”状态；唯一真相在 Service（当前实现中部分状态在内存并与 DB 同步，可逐步迁为全 DB）。

## 调用关系

| Service | 能力 | AI（经 CLI） | UI（经 UIController） |
|---------|------|--------------|------------------------|
| **IAiActionService** | act（Place/Referee/Commander/Creator 等） | cli act → POST /api/UI/act | UIController 调用 |
| **IWorldState** | 快照/规则/落子/裁判/开局/重置 | cli next/state/snapshot/… → /api/UI/* | UIController 调用 |
| **INarrativeService** | 叙事 | act 盘活/造人、reset | UIController 调用 |

所有端点统一为 **api/UI**，由单一 UIController 提供。
