#!/usr/bin/env bash
# 根目录一键启动：先清理端口/进程占用，再启动前后端，日志从本 sh 输出，Ctrl+C 退出并停掉前后端。
# 跨平台：Windows（需 Git Bash / WSL / MSYS）与 Linux/macOS 均可运行。
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$REPO_ROOT/backend/GomokuSandbox.Api"
FRONTEND_DIR="$REPO_ROOT/frontend"
BACKEND_PORT=5244
FRONTEND_PORT=3001

# 是否在 Windows 环境（Git Bash / MSYS 等）：用 taskkill 才能可靠结束进程
is_windows() { [[ -n "$WINDIR" ]] || [[ "$OSTYPE" =~ ^(msys|cygwin) ]]; }

# 结束占用指定端口的进程，避免打不开或多开
kill_port() {
  local port=$1
  local pids=""
  if command -v lsof &>/dev/null; then
    pids=$(lsof -ti ":$port" 2>/dev/null || true)
  else
    # Windows 无 lsof 时用 netstat（最后一列为 PID）
    pids=$(netstat -ano 2>/dev/null | grep ":$port" | grep LISTENING | awk '{print $NF}' | sort -u || true)
  fi
  if [[ -n "$pids" ]]; then
    echo "[run] 发现端口 $port 已被占用 (PID: $pids)，正在结束进程..."
    for pid in $pids; do
      if is_windows; then
        taskkill //F //PID "$pid" 2>/dev/null || true
      else
        kill -9 "$pid" 2>/dev/null || true
      fi
    done
    sleep 2
  fi
}

echo "[run] 检查端口与进程占用（避免打不开或多开）..."
kill_port "$BACKEND_PORT"
kill_port "$FRONTEND_PORT"

# Ctrl+C 或脚本退出时停掉前后端（Windows 下用 taskkill 才能可靠结束 dotnet/node）
cleanup() {
  echo ""
  echo "[run] 正在停止前后端..."
  if is_windows; then
    taskkill //F //PID $BACKEND_PID 2>/dev/null || true
    taskkill //F //PID $FRONTEND_PID 2>/dev/null || true
    kill_port "$BACKEND_PORT"
    kill_port "$FRONTEND_PORT"
  else
    kill $BACKEND_PID $FRONTEND_PID 2>/dev/null || true
  fi
  trap - INT TERM EXIT
  exit 0
}
trap cleanup INT TERM EXIT

# 后台启动后端，日志直接打到当前终端（带前缀便于区分）
echo "[run] 启动后端 (dotnet run)，日志见下方 [backend]..."
( cd "$BACKEND_DIR" && dotnet run 2>&1 | sed 's/^/[backend] /' ) &
BACKEND_PID=$!

# 后台启动前端，日志直接打到当前终端（带前缀便于区分）
echo "[run] 启动前端 (pnpm dev)，日志见下方 [frontend]..."
( cd "$FRONTEND_DIR" && pnpm dev 2>&1 | sed 's/^/[frontend] /' ) &
FRONTEND_PID=$!

echo ""
echo "=========================================="
echo "[run] 前后端已启动，日志在下方。按 Ctrl+C 退出并停止前后端。"
echo "  后端: http://localhost:$BACKEND_PORT  前端: http://localhost:$FRONTEND_PORT"
echo "=========================================="
echo ""

# 保持脚本运行，直到收到 Ctrl+C 或任一子进程退出
wait
