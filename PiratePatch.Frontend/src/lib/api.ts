export interface SiteConfig {
  botUsername: string
  publicKey: string
  devLogin: boolean
}

export interface Player {
  handle: string
}

export interface Solve {
  rank: number
  solvedAt: string
  certificateUrl: string
}

export interface Me {
  player: Player | null
  solve: Solve | null
}

export interface LeaderboardEntry {
  rank: number
  handle: string
  solvedAt: string
  certificateUrl: string
}

export interface Leaderboard {
  total: number
  entries: LeaderboardEntry[]
}

interface Problem {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message)
  }
}

const FALLBACK = "Avast ye! Something went adrift. Try again in a moment."

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(`/api${path}`, {
      ...init,
      headers: { ...(init?.body ? { 'Content-Type': 'application/json' } : {}), ...init?.headers },
    })
  } catch {
    throw new ApiError(0, "Can't reach the harbour. Check yer connection.")
  }

  if (response.ok) {
    return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
  }

  let problem: Problem = {}
  try {
    problem = (await response.json()) as Problem
  } catch {
    // not JSON; fall through to the generic message
  }

  const message =
    Object.values(problem.errors ?? {}).flat()[0] ??
    problem.detail ??
    (response.status === 429 ? "Too many requests. Slow yer oars, matey." : FALLBACK)
  throw new ApiError(response.status, message)
}

const post = <T>(path: string, body?: unknown) =>
  request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })

export const api = {
  config: () => request<SiteConfig>('/config'),
  me: () => request<Me>('/me'),
  loginWithTelegram: (payload: Record<string, unknown>) => post<Me>('/auth/telegram', payload),
  loginForDevelopment: (handle: string) => post<Me>('/auth/dev', { handle }),
  logout: () => post<void>('/auth/logout'),
  submit: (code: string) => post<{ success: boolean; solve: Solve | null }>('/challenge/submit', { code }),
  leaderboard: (limit = 100, offset = 0) => request<Leaderboard>(`/leaderboard?limit=${limit}&offset=${offset}`),
}

export const BINARY_URL = '/api/challenge/binary'
