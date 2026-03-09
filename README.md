# Gomoku Sandbox（五子棋沙盘）

一个由 AI 驱动的五子棋沙盘世界：统领者设定规则，造人者创造黑/白双方选手，双方按规则对弈，胜负由后端算法判定。

## 技术栈

- **后端**：.NET 8，ASP.NET Core Web API，EF Core，SQLite
- **前端**：Nuxt 3 + Vue 3 + Pinia
- **CLI**：dotnet 控制台，供 AI/脚本按步骤驱动世界

## 快速开始

### 一键启动（推荐）

在仓库根目录执行（需 Bash 环境，Windows 可用 Git Bash / WSL）：

```bash
./run.sh
```

脚本会先清理端口占用，再启动后端与前端，日志输出到当前终端；按 **Ctrl+C** 可退出并停止前后端。

- 后端：<http://localhost:5244>
- 前端：<http://localhost:3001>

### 单独启动

- 后端：`cd backend/GomokuSandbox.Api && dotnet run`
- 前端：`cd frontend && pnpm install && pnpm dev`

## 项目结构

```
├── backend/           # .NET 后端
│   ├── GomokuSandbox.Api   # Web API
│   ├── GomokuSandbox.Service  # 业务与数据
│   ├── GomokuSandbox.Spec   # CLI、模型与规格
│   └── ...
├── frontend/          # Nuxt 3 前端
├── docs/              # 说明文档（如 AI 驱动流程）
├── run.sh             # 一键启动脚本（前后端 + 端口清理）
└── README.md
```

## 使用说明

- **浏览器**：打开前端页面即可查看棋盘、对局状态并与 API 交互。
- **AI 驱动**：参见 `docs/AI-AGENT-GUIDE.md`，通过 CLI `dotnet run`（在 `backend/GomokuSandbox.Cli`）按「统领者 → 造人者 → 造人 → 开局 → 下棋」的顺序驱动世界；每步以 JSON 指定代理角色与参数。

## 环境要求

- .NET 8 SDK
- Node.js + pnpm（前端）
- 运行 `run.sh` 需 Bash（Windows 下 Git Bash / WSL / MSYS 等）
