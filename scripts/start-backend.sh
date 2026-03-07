#!/usr/bin/env bash
set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND_DIR="$REPO_ROOT/backend/GomokuSandbox.Api"
FRONTEND_API_CLIENT="$REPO_ROOT/frontend/lib/api-client/generated.ts"
PORT=5244
SWAGGER_URL="http://localhost:$PORT/swagger/v1/swagger.json"

# 检查并结束占用端口的进程
kill_port() {
  local pids
  pids=$(lsof -ti ":$PORT" 2>/dev/null || true)
  if [[ -n "$pids" ]]; then
    echo "[start-backend] 发现端口 $PORT 已被占用 (PID: $pids)，正在结束进程..."
    echo "$pids" | xargs kill -9 2>/dev/null || true
    sleep 1
  fi
}

# 等待 Swagger 可访问
wait_for_swagger() {
  local max=30
  local n=0
  echo "[start-backend] 等待 Swagger 就绪: $SWAGGER_URL"
  until curl -s -o /dev/null -w "%{http_code}" "$SWAGGER_URL" | grep -q 200; do
    n=$((n + 1))
    if [[ $n -ge $max ]]; then
      echo "[start-backend] 超时：Swagger 未在 ${max}s 内就绪" >&2
      exit 1
    fi
    sleep 1
  done
  echo "[start-backend] Swagger 已就绪"
}

# 生成 SDK 并同步到 Vue（使用 backend 内的 dotnet tool）
generate_and_sync_client() {
  echo "[start-backend] 正在生成 API 客户端并写入 Vue 项目..."
  (cd "$BACKEND_DIR" && dotnet nswag openapi2tsclient "/Input:$SWAGGER_URL" "/Output:../../frontend/lib/api-client/generated.ts" /ClassName:ApiClient) || {
    echo "[start-backend] NSwag 生成失败；请运行 cd $BACKEND_DIR && dotnet tool restore" >&2
    return 0
  }
  echo "[start-backend] 已同步到 $FRONTEND_API_CLIENT"
}

cd "$REPO_ROOT"
kill_port

cd "$BACKEND_DIR"
echo "[start-backend] 在后台启动后端 (dotnet run)..."
dotnet run &
DOTNET_PID=$!
trap "kill $DOTNET_PID 2>/dev/null || true" EXIT

wait_for_swagger
generate_and_sync_client

# 结束当前后台 dotnet，下面用前台方式启动以便看到日志
kill $DOTNET_PID 2>/dev/null || true
trap - EXIT
sleep 1
kill_port

echo "[start-backend] 启动后端 (dotnet run，前台)..."
exec dotnet run
