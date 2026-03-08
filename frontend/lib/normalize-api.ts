/**
 * 统一处理后端 API 返回的 PascalCase / camelCase 兼容。
 * 后端 .NET 默认序列化为 PascalCase，前端约定使用 camelCase，此处做一次归一化，后续代码只读 camelCase。
 */

/** 从对象上取属性，优先 camelCase，否则 PascalCase */
function pick<T>(obj: Record<string, unknown> | null | undefined, camelKey: string): T | undefined {
  if (obj == null) return undefined
  const pascalKey = camelKey.charAt(0).toUpperCase() + camelKey.slice(1)
  return (obj[camelKey] ?? obj[pascalKey]) as T | undefined
}

/** 从对象上取属性，若不存在则返回 defaultValue */
function pickOr<T>(obj: Record<string, unknown> | null | undefined, camelKey: string, defaultValue: T): T {
  const v = pick<T>(obj, camelKey)
  return v !== undefined && v !== null ? v : defaultValue
}

export interface NormalizedSnapshot {
  boardSize: number
  board: number[][]
  currentTurn: string
  moveCount: number
  gameStatus: string
  winner?: string
  lastMoveX?: number | null
  lastMoveY?: number | null
  gameStartedAt?: string | null
  gameFinishedAt?: string | null
}

export interface NormalizedPlayerView {
  id: number
  color: string
  intelligence: number
  score: number
  createdAt?: string
}

export interface NormalizedNarrativeEntry {
  role: string
  message: string
  at: string
}

export interface NormalizedWorldView {
  snapshot: NormalizedSnapshot | null
  rules: { minMovesBeforeWin?: number; blackAdvantage?: number; direction?: string }
  direction: string
  blackPlayer: NormalizedPlayerView | null
  whitePlayer: NormalizedPlayerView | null
  narrative: NormalizedNarrativeEntry[]
  /** 待裁判判定时，为声称赢的一方（Black/White）；否则为 null。用于显示「我赢了没？」/「等待裁判」 */
  pendingRefereeBy: string | null
}

function normalizeSnapshot(raw: Record<string, unknown> | null | undefined): NormalizedSnapshot | null {
  const sn = pick<Record<string, unknown>>(raw, 'snapshot') ?? pick<Record<string, unknown>>(raw, 'Snapshot')
  if (!sn || typeof sn !== 'object') return null
  const board = pick<number[][]>(sn, 'board') ?? pick<number[][]>(sn, 'Board')
  return {
    boardSize: pickOr(sn, 'boardSize', 15),
    board: Array.isArray(board) ? board : [],
    currentTurn: pickOr(sn, 'currentTurn', ''),
    moveCount: pickOr(sn, 'moveCount', 0),
    gameStatus: pickOr(sn, 'gameStatus', ''),
    winner: pick(sn, 'winner'),
    lastMoveX: pick(sn, 'lastMoveX'),
    lastMoveY: pick(sn, 'lastMoveY'),
    gameStartedAt: pick(sn, 'gameStartedAt') ?? pick(sn, 'GameStartedAt'),
    gameFinishedAt: pick(sn, 'gameFinishedAt') ?? pick(sn, 'GameFinishedAt'),
  }
}

function normalizePlayer(raw: Record<string, unknown> | null | undefined): NormalizedPlayerView | null {
  if (!raw || typeof raw !== 'object') return null
  return {
    id: pickOr(raw, 'id', 0),
    color: pickOr(raw, 'color', ''),
    intelligence: pickOr(raw, 'intelligence', 0),
    score: pickOr(raw, 'score', 0),
    createdAt: pick(raw, 'createdAt') ?? pick(raw, 'CreatedAt') as string | undefined,
  }
}

function normalizeNarrativeEntry(raw: unknown): NormalizedNarrativeEntry | null {
  if (!raw || typeof raw !== 'object') return null
  const o = raw as Record<string, unknown>
  const role = pick(o, 'role') ?? pick(o, 'Role')
  const message = pick(o, 'message') ?? pick(o, 'Message')
  const at = pick(o, 'at') ?? pick(o, 'At')
  if (role == null || message == null) return null
  return { role: String(role), message: String(message), at: at != null ? String(at) : '' }
}

/**
 * 将 /api/UI/view 的原始响应归一化为前端约定的 camelCase 结构。
 * 顶层与嵌套字段均兼容 PascalCase，调用方无需再关心大小写。
 */
export function normalizeWorldView(raw: Record<string, unknown> | null | undefined): NormalizedWorldView {
  if (!raw || typeof raw !== 'object') {
    return {
      snapshot: null,
      rules: {},
      direction: '',
      blackPlayer: null,
      whitePlayer: null,
      narrative: [],
      pendingRefereeBy: null,
    }
  }
  const blackRaw = pick<Record<string, unknown>>(raw, 'blackPlayer') ?? pick<Record<string, unknown>>(raw, 'BlackPlayer')
  const whiteRaw = pick<Record<string, unknown>>(raw, 'whitePlayer') ?? pick<Record<string, unknown>>(raw, 'WhitePlayer')
  const narrativeRaw = pick<unknown[]>(raw, 'narrative') ?? pick<unknown[]>(raw, 'Narrative') ?? []
  const rulesRaw = pick<Record<string, unknown>>(raw, 'rules') ?? pick<Record<string, unknown>>(raw, 'Rules') ?? {}
  const snapshot = normalizeSnapshot(raw)
  const narrative: NormalizedNarrativeEntry[] = []
  for (const item of narrativeRaw) {
    const entry = normalizeNarrativeEntry(item)
    if (entry) narrative.push(entry)
  }
  const pendingRefereeBy = (pick(raw, 'pendingRefereeBy') ?? pick(raw, 'PendingRefereeBy')) as string | null | undefined
  return {
    snapshot,
    rules: {
      minMovesBeforeWin: pick(rulesRaw, 'minMovesBeforeWin') ?? pick(rulesRaw, 'MinMovesBeforeWin'),
      blackAdvantage: pick(rulesRaw, 'blackAdvantage') ?? pick(rulesRaw, 'BlackAdvantage'),
      direction: pick(rulesRaw, 'direction') ?? pick(rulesRaw, 'Direction') as string | undefined,
    },
    direction: (pick(raw, 'direction') ?? pick(raw, 'Direction')) as string ?? '',
    blackPlayer: blackRaw ? normalizePlayer(blackRaw) : null,
    whitePlayer: whiteRaw ? normalizePlayer(whiteRaw) : null,
    narrative,
    pendingRefereeBy: pendingRefereeBy === 'Black' || pendingRefereeBy === 'White' ? pendingRefereeBy : null,
  }
}
