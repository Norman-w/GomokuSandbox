<template>
  <div class="page">
    <h1>五子棋 Gomoku Sandbox</h1>
    <div class="main">
      <div class="left">
        <p class="status">{{ statusLabel }} · 当前 {{ turnLabel }} 落子 · 共 {{ snapshot?.moveCount ?? 0 }} 步</p>
        <p v-if="snapshot?.gameStartedAt" class="game-time">
          <span>开局时间：{{ formatGameTime(snapshot.gameStartedAt) }}</span>
          <template v-if="snapshot?.gameStatus === 'Playing'">
            <span class="elapsed"> · 进行中：{{ elapsedDuration }}</span>
          </template>
          <template v-else-if="snapshot?.gameFinishedAt && snapshot?.gameStartedAt">
            <span class="total"> · 本局用时：{{ formatDuration(snapshot.gameStartedAt, snapshot.gameFinishedAt) }}</span>
          </template>
        </p>
        <div class="board-wrap">
          <div
            class="board"
            :style="{ '--size': boardSize }"
          >
            <template v-for="(row, i) in boardGrid" :key="i">
              <button
                v-for="(cell, j) in row"
                :key="j"
                class="cell"
                :class="{
                  black: cell === 1,
                  white: cell === 2,
                  'last-placed': snapshot?.lastMoveX === i && snapshot?.lastMoveY === j
                }"
                :disabled="!!cell || snapshot?.gameStatus !== 'Playing' || !!view?.pendingRefereeBy || !snapshot?.currentTurn || (snapshot?.currentTurn !== 'Black' && snapshot?.currentTurn !== 'White')"
                @click="place(i, j)"
              >
                <span v-if="cell" class="stone" :class="cell === 1 ? 'black' : 'white'" />
              </button>
            </template>
          </div>
        </div>
        <div class="actions">
          <button type="button" @click="fetchView">刷新</button>
        </div>
      </div>
      <aside class="right">
        <!-- 对局双方：世界/对局未开始则无棋手，显示未加入；有对局时才显示棋手信息（上帝视角） -->
        <section class="panel players">
          <h2>对局双方 <span class="god-mode-tag">上帝视角</span></h2>
          <p v-if="!view?.blackPlayer && !view?.whitePlayer" class="muted hint">对局未开始或已重置，双方未加入。</p>
          <div class="player-cards">
            <div
              class="player-card black"
              :class="{ 'is-turn': (snapshot?.currentTurn === 'Black' && snapshot?.gameStatus === 'Playing' && !view?.pendingRefereeBy) || view?.pendingRefereeBy === 'Black', 'is-winner': snapshot?.winner === 'Black' }"
            >
              <div class="player-label">黑方 <span v-if="snapshot?.winner === 'Black'" class="trophy" title="本局获胜">🏆</span></div>
              <template v-if="view?.blackPlayer">
                <div class="player-info">智商 {{ view.blackPlayer.intelligence }} · 积分 {{ view.blackPlayer.score }}</div>
                <div class="player-info god-mode">ID {{ view.blackPlayer.id }} · 创建 {{ formatCreatedAt(view.blackPlayer.createdAt) }}</div>
                <div v-if="view?.pendingRefereeBy === 'Black'" class="turn-badge">我赢了没？</div>
                <div v-else-if="view?.pendingRefereeBy === 'White'" class="turn-badge waiting">等待裁判</div>
                <div v-else-if="snapshot?.currentTurn === 'Black' && snapshot?.gameStatus === 'Playing'" class="turn-badge">轮到我了</div>
              </template>
              <template v-else>
                <div class="player-info not-joined">未加入</div>
              </template>
            </div>
            <div
              class="player-card white"
              :class="{ 'is-turn': (snapshot?.currentTurn === 'White' && snapshot?.gameStatus === 'Playing' && !view?.pendingRefereeBy) || view?.pendingRefereeBy === 'White', 'is-winner': snapshot?.winner === 'White' }"
            >
              <div class="player-label">白方 <span v-if="snapshot?.winner === 'White'" class="trophy" title="本局获胜">🏆</span></div>
              <template v-if="view?.whitePlayer">
                <div class="player-info">智商 {{ view.whitePlayer.intelligence }} · 积分 {{ view.whitePlayer.score }}</div>
                <div class="player-info god-mode">ID {{ view.whitePlayer.id }} · 创建 {{ formatCreatedAt(view.whitePlayer.createdAt) }}</div>
                <div v-if="view?.pendingRefereeBy === 'White'" class="turn-badge">我赢了没？</div>
                <div v-else-if="view?.pendingRefereeBy === 'Black'" class="turn-badge waiting">等待裁判</div>
                <div v-else-if="snapshot?.currentTurn === 'White' && snapshot?.gameStatus === 'Playing'" class="turn-badge">轮到我了</div>
              </template>
              <template v-else>
                <div class="player-info not-joined">未加入</div>
              </template>
            </div>
          </div>
        </section>
        <!-- 统领者与统领者的思想：只展示叙事里记录的发话，不展示后端默认规则 -->
        <section class="panel commander">
          <h2>统领者</h2>
          <div class="commander-thought">
            <template v-if="commanderEntries.length">
              <div v-for="(entry, idx) in commanderEntries" :key="idx" class="narrative-entry">{{ entry.message }}</div>
            </template>
            <p v-else class="muted">统领者尚未发话</p>
          </div>
        </section>
        <!-- 造人者干了啥、怎么想的 -->
        <section class="panel creator">
          <h2>造人者</h2>
          <div class="creator-log">
            <template v-if="creatorEntries.length">
              <div v-for="(entry, idx) in creatorEntries" :key="idx" class="narrative-entry">
                {{ entry.message }}
              </div>
            </template>
            <p v-else class="muted">造人者尚未有记录</p>
          </div>
        </section>
        <div class="panel-actions">
          <button type="button" class="btn-reset" @click="resetWorld">重置世界</button>
        </div>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { normalizeWorldView, type NormalizedWorldView } from '~/lib/normalize-api'

const config = useRuntimeConfig()
const apiBase = config.public.apiBase as string

const view = ref<NormalizedWorldView | null>(null)

// 无数据时也显示 15x15 空棋盘，避免只显示一块色块
const boardGrid = computed(() => {
  const b = view.value?.snapshot?.board
  if (b?.length) return b
  return Array(15).fill(null).map(() => Array(15).fill(0))
})
const boardSize = computed(() => view.value?.snapshot?.boardSize ?? 15)
const snapshot = computed(() => view.value?.snapshot)
// 无对局时显示「未开始」且不显示黑方落子，避免与真实对局混淆
const statusLabel = computed(() => {
  const s = snapshot.value
  if (!s) return '—'
  if (s.gameStatus === 'Idle' && (s.moveCount ?? 0) === 0) return '未开始'
  return s.gameStatus ?? '—'
})
const turnLabel = computed(() => {
  const s = snapshot.value
  if (!s) return '—'
  if (s.gameStatus === 'Idle' && (s.moveCount ?? 0) === 0) return '—'
  return s.currentTurn || '—'
})

// 叙事已在 normalizeWorldView 中统一为 camelCase，直接按 role/message 使用
const commanderEntries = computed(() =>
  view.value?.narrative?.filter(e => e.role === 'Commander') ?? []
)
const creatorEntries = computed(() =>
  view.value?.narrative?.filter(e => e.role === 'Creator') ?? []
)

// 本局进行时间（仅对局中有效，刷新 view 后仍会更新）
const elapsedDuration = ref('0:00')
const elapsedTimer = ref<ReturnType<typeof setInterval> | null>(null)
watch(
  () => view.value?.snapshot?.gameStartedAt && view.value?.snapshot?.gameStatus === 'Playing',
  (isPlaying) => {
    if (elapsedTimer.value) { clearInterval(elapsedTimer.value); elapsedTimer.value = null }
    if (!isPlaying) return
    const start = view.value?.snapshot?.gameStartedAt
    if (!start) return
    const update = () => {
      const startMs = parseUtc(start)
      const sec = Math.floor((Date.now() - startMs) / 1000)
      const m = Math.floor(sec / 60)
      const s = sec % 60
      elapsedDuration.value = `${m}:${s.toString().padStart(2, '0')}`
    }
    update()
    elapsedTimer.value = setInterval(update, 1000)
  },
  { immediate: true }
)
onUnmounted(() => { if (elapsedTimer.value) clearInterval(elapsedTimer.value) })

async function fetchView(opts?: { logWorldResponse?: boolean }) {
  try {
    const r = await fetch(`${apiBase}/api/UI/view`)
    if (!r.ok) return
    const raw = await r.json()
    if (opts?.logWorldResponse) {
      console.log('[世界] 完整返回（/api/UI/view）:', JSON.stringify(raw, null, 2))
    }
    view.value = normalizeWorldView(raw)
  } catch (e) {
    console.error(e)
  }
}

/** 后端返回 UTC 时间但常不带 Z，按 UTC 解析再用于计算/显示，避免 GMT+8 等时区多出 8 小时 */
function parseUtc(iso: string): number {
  const s = iso.trim()
  const hasTz = s.endsWith('Z') || /[+-]\d{2}:?\d{2}$/.test(s)
  return new Date(hasTz ? s : s + 'Z').getTime()
}

function formatGameTime(iso?: string | null): string {
  if (!iso) return '—'
  try {
    return new Date(parseUtc(iso)).toLocaleString('zh-CN', { dateStyle: 'short', timeStyle: 'short' })
  } catch {
    return '—'
  }
}

function formatDuration(startIso: string, endIso: string): string {
  try {
    const s = parseUtc(startIso)
    const e = parseUtc(endIso)
    const sec = Math.floor((e - s) / 1000)
    const m = Math.floor(sec / 60)
    const s_ = sec % 60
    return `${m} 分 ${s_} 秒`
  } catch {
    return '—'
  }
}

async function resetWorld() {
  try {
    const r = await fetch(`${apiBase}/api/UI/reset`, { method: 'POST' })
    if (r.ok) {
      await new Promise(r => setTimeout(r, 150))
      await fetchView({ logWorldResponse: true })
    } else {
      alert('重置失败')
    }
  } catch (e) {
    console.error(e)
  }
}

function formatCreatedAt(createdAt?: string): string {
  if (!createdAt) return '—'
  try {
    return new Date(parseUtc(createdAt)).toLocaleString('zh-CN', { dateStyle: 'short', timeStyle: 'short' })
  } catch {
    return '—'
  }
}

async function place(x: number, y: number) {
  const turn = view.value?.snapshot?.currentTurn
  if (!turn) return
  try {
    const r = await fetch(`${apiBase}/api/UI/place`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ color: turn, x, y }),
    })
    if (r.ok) {
      await fetchView()
    } else {
      const text = await r.text()
      alert(text || '落子失败')
    }
  } catch (e) {
    console.error(e)
  }
}

const POLL_INTERVAL_MS = 2000
let pollTimer: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  fetchView()
  pollTimer = setInterval(fetchView, POLL_INTERVAL_MS)
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<style scoped>
.page { padding: 1rem; max-width: 1200px; margin: 0 auto; background: #fff; min-height: 100vh; }
.main { display: flex; gap: 1.5rem; align-items: flex-start; flex-wrap: nowrap; }
.left { flex: 0 0 auto; }
.right { flex: 1 1 280px; min-width: 280px; max-width: 400px; display: flex; flex-direction: column; gap: 1rem; }
@media (max-width: 720px) {
  .main { flex-wrap: wrap; }
  .right { max-width: none; }
}

.status { margin: 0.5rem 0; }
.board-wrap { margin: 1rem 0; }

.board {
  display: grid;
  grid-template-columns: repeat(var(--size), minmax(0, 1fr));
  grid-template-rows: repeat(var(--size), minmax(0, 1fr));
  gap: 0;
  width: min(90vw, 400px);
  aspect-ratio: 1;
  border: 1px solid #333;
  background: #dcb35c;
}
.cell {
  aspect-ratio: 1;
  min-width: 0;
  border-right: 1px solid #333;
  border-bottom: 1px solid #333;
  background: transparent;
  cursor: pointer;
  padding: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
}
.cell:nth-child(15n) { border-right: none; }
.cell:nth-last-child(-n+15) { border-bottom: none; }
.cell:hover:not(:disabled) { background: rgba(0,0,0,0.06); }
.cell:disabled { cursor: default; }
.stone {
  position: absolute;
  width: 78%;
  height: 78%;
  border-radius: 50%;
  pointer-events: none;
}
.stone.black { background: #1a1a1a; box-shadow: 0 1px 2px rgba(0,0,0,0.3); }
.stone.white { background: #f5f5f5; box-shadow: 0 1px 2px rgba(0,0,0,0.2); border: 1px solid #ccc; }
/* 最后一子持续闪烁 */
.cell.last-placed .stone {
  animation: stone-flash 1s ease-in-out infinite;
}
@keyframes stone-flash {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.4; transform: scale(1.12); }
}

.actions { margin-top: 1rem; display: flex; gap: 0.5rem; }
.actions button { padding: 0.4rem 0.8rem; cursor: pointer; }

/* 右侧面板 */
.panel {
  background: #f8f8f8;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 0.75rem 1rem;
}
.panel h2 { margin: 0 0 0.5rem; font-size: 0.95rem; color: #333; display: flex; align-items: center; gap: 0.35rem; }
.god-mode-tag { font-size: 0.7rem; color: #888; font-weight: normal; }
.player-info.god-mode { font-size: 0.75rem; opacity: 0.75; margin-top: 0.15rem; }

.player-cards { display: flex; flex-direction: column; gap: 0.5rem; }
.player-card {
  padding: 0.6rem 0.8rem;
  border-radius: 6px;
  border: 2px solid transparent;
  transition: border-color 0.2s, box-shadow 0.2s;
}
.player-card.black { background: #2a2a2a; color: #eee; }
.player-card.white { background: #f0f0f0; color: #222; border: 1px solid #ccc; }
.player-card.is-turn { border-color: #c9302c; box-shadow: 0 0 0 2px rgba(201,48,44,0.3); }
.player-card.is-winner { box-shadow: 0 0 0 2px rgba(212,175,55,0.6); }
.player-label { font-weight: 600; margin-bottom: 0.25rem; }
.trophy { margin-left: 0.25rem; font-size: 1rem; }
.player-info { font-size: 0.85rem; opacity: 0.9; }
.turn-badge {
  margin-top: 0.35rem;
  display: inline-block;
  padding: 0.2rem 0.5rem;
  background: #c9302c;
  color: #fff;
  font-size: 0.8rem;
  border-radius: 4px;
  font-weight: 600;
}
.player-card.white .turn-badge { background: #286090; }

.commander-thought .narrative-entry { margin: 0.35rem 0; font-size: 0.9rem; line-height: 1.45; white-space: pre-wrap; word-break: break-word; padding-left: 0.5rem; border-left: 3px solid #5bc0de; }
.creator-log .narrative-entry { margin: 0.35rem 0; font-size: 0.85rem; line-height: 1.4; padding-left: 0.5rem; border-left: 3px solid #5bc0de; }
.muted { margin: 0; font-size: 0.85rem; color: #888; }
.players .hint { margin-bottom: 0.5rem; font-size: 0.8rem; }
.player-info.not-joined { font-style: italic; color: #999; }
.game-time { margin: 0.25rem 0; font-size: 0.9rem; color: #555; }
.game-time .elapsed { color: #286090; }
.game-time .total { color: #2a2a2a; font-weight: 500; }
.panel-actions { margin-top: 0.25rem; }
.btn-reset { padding: 0.35rem 0.6rem; font-size: 0.8rem; color: #666; background: #eee; border: 1px solid #ccc; border-radius: 4px; cursor: pointer; }
.btn-reset:hover { background: #e0e0e0; }
</style>
