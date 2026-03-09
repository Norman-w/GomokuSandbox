#!/usr/bin/env bash
# 根目录一键启动：先清理端口/进程占用，再后台启动后端与前端，退出前显示 PID 与状态。
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$REPO_ROOT/backend/GomokuSandbox.Api"
FRONTEND_DIR="$REPO_ROOT/frontend"
BACKEND_PORT=5244
FRONTEND_PORT=3001

# 结束占用指定端口的进程，避免打不开或多开
kill_port() {
  local port=$1
  local pids=""
  if command -v lsof &>/dev/null; then
    pids=$(lsof -ti ":$port" 2>/dev/null || true)
  else
    # Windows Git Bash 等无 lsof 时用 netstat
    pids=$(netstat -ano 2>/dev/null | grep -E ":$port\\s+.*LISTENING" | awk '{print $NF}' | sort -u || true)
  fi
  if [[ -n "$pids" ]]; then
    echo "[run] 发现端口 $port 已被占用 (PID: $pids)，正在结束进程..."
    for pid in $pids; do
      kill -9 "$pid" 2>/dev/null || true
    done
    sleep 1
  fi
}

echo "[run] 检查端口与进程占用（避免打不开或多开）..."
kill_port "$BACKEND_PORT"
kill_port "$FRONTEND_PORT"

# 后台启动后端（不阻塞，退出后继续运行）
echo "[run] 在后台启动后端 (dotnet run)..."
(
  cd "$BACKEND_DIR"
  nohup dotnet run >> "$REPO_ROOT/backend.log" 2>&1
) &
BACKEND_PID=$!
disown $BACKEND_PID 2>/dev/null || true

# 后台启动前端（不阻塞，退出后继续运行）
echo "[run] 在后台启动前端 (pnpm dev)..."
(
  cd "$FRONTEND_DIR"
  nohup pnpm dev >> "$REPO_ROOT/frontend.log" 2>&1
) &
FRONTEND_PID=$!
disown $FRONTEND_PID 2>/dev/null || true

sleep 2

# 显示进程 ID 和状态
echo ""
echo "=========================================="
echo "[run] 已启动，进程 ID 与状态："
echo "=========================================="
if kill -0 "$BACKEND_PID" 2>/dev/null; then
  echo "  后端 (dotnet):  PID $BACKEND_PID  状态: 运行中"
else
  echo "  后端 (dotnet):  PID $BACKEND_PID  状态: 已退出（请查看 $REPO_ROOT/backend.log）"
fi
if kill -0 "$FRONTEND_PID" 2>/dev/null; then
  echo "  前端 (pnpm):    PID $FRONTEND_PID  状态: 运行中"
else
  echo "  前端 (pnpm):    PID $FRONTEND_PID  状态: 已退出（请查看 $REPO_ROOT/frontend.log）"
fi
echo "=========================================="
echo "  后端地址: http://localhost:$BACKEND_PORT"
echo "  前端地址: http://localhost:$FRONTEND_PORT"
echo "  停止: kill $BACKEND_PID $FRONTEND_PID"
echo "=========================================="
