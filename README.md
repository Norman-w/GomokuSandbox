# Gomoku Sandbox（五子棋沙盘）

一个由 AI 驱动的五子棋沙盘世界：统领者设定规则，造人者创造黑/白双方选手，双方按规则对弈，胜负由后端算法判定。AI 作为「单脑扮多人」依次代理统领者、造人者、黑方、白方，你在 UI 里看结果。

## 技术栈

- **后端**：.NET 8，ASP.NET Core Web API，EF Core，SQLite
- **前端**：Nuxt 3 + Vue 3 + Pinia

## 快速开始

### 1. 启动前后端（只是把服务跑起来）

在仓库根目录执行（需 Bash 环境，Windows 可用 Git Bash / WSL）：

```bash
./run.sh
```

**说明**：启动后你只是有了 UI，可以在浏览器里看到棋盘和对局状态；此时还没有 AI 在驱动世界，要「下一局」需要让 AI 介入

### 2. 让 AI 介入，完成一局

在 **IDE 里新开一个 Chat**，让 Chat 代替你去执行 CLI 命令，它会按「统领者发言 → 造人者造人 → 开局 → 下棋」跑完一整局；你在 **UI 里看它们下完** 即可。

- **Cursor**：在新对话里输入并发送：`@AI docs/AI-AGENT-GUIDE.md` ，让 AI 按那个文档的说明执行 CLI。
- **VS Code**：在新对话里用：`# AI docs/AI-AGENT-GUIDE.md` ，同样让 AI 按那个文档执行 CLI。

CLI 会运行：`dotnet run --project backend/GomokuSandbox.Cli`（无参数看 info也就是学技能，有参数即扮演）。

### 已知问题

部分 AI 模型不听话，下着下着会停，目前没有完美解决办法，仅靠提示词有时也无效。

## 项目结构

```
├── backend/
│   ├── GomokuSandbox.Api      # 给你用的，Web API（前端页面供给和页面内的接口）
│   ├── GomokuSandbox.Cli      # 给AI用的，CLI：无参数 看info学知识，有参数扮演
│   ├── GomokuSandbox.Service  # 底层，业务与数据
│   ├── GomokuSandbox.Spec     # 底层，模型、规格与 CLI 命令定义
│   └── ...
├── frontend/                  # Nuxt 3 前端（棋盘与状态展示）
├── docs/
│   └── AI-AGENT-GUIDE.md     # 给 AI 的说明：角色、顺序、如何用 CLI
├── run.sh                     # 一键启动后端+前端（端口清理）
└── README.md
```

## 环境要求

- .NET 8 SDK
- Node.js + pnpm（前端）
- 运行 `run.sh` 需 Bash（Windows 下 Git Bash / WSL / MSYS 等）
