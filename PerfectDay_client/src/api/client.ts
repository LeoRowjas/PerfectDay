import type {
  Chart,
  ChartScale,
  Credentials,
  Dashboard,
  Profile,
  SaveTaskRequest,
  Schedule,
  Task,
  UpdateProfileRequest,
} from './types'

const FALLBACK_ERROR = 'Не удалось выполнить запрос. Попробуйте ещё раз.'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// Заголовки ошибок фреймворка приходят на английском, поэтому для них есть свои тексты.
const STATUS_MESSAGES: Record<number, string> = {
  401: 'Сессия истекла. Войдите снова.',
  403: 'Это действие вам недоступно.',
  404: 'Это действие сейчас недоступно. Обновите страницу, а если ошибка повторится, перезапустите сервер.',
  405: 'Это действие сейчас недоступно. Обновите страницу, а если ошибка повторится, перезапустите сервер.',
  429: 'Слишком много попыток. Подождите минуту.',
}

const hasCyrillic = (text: string) => /[а-яё]/i.test(text)

interface ProblemDetails {
  title?: string
  errors?: Record<string, string[]>
}

export function problemMessage(status: number, problem: ProblemDetails | null): string {
  const fieldError = problem?.errors && Object.values(problem.errors).flat().find(hasCyrillic)
  if (fieldError) return fieldError

  const title = problem?.title
  if (status < 500 && title && hasCyrillic(title)) return title
  return STATUS_MESSAGES[status] ?? FALLBACK_ERROR
}

async function request<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  let response: Response
  try {
    response = await fetch(`/api${path}`, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'Нет связи с сервером.')
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null
    throw new ApiError(response.status, problemMessage(response.status, problem))
  }

  return (response.status === 204 ? undefined : await response.json()) as T
}

export const api = {
  register: (credentials: Credentials) => request<Profile>('/auth/register', 'POST', credentials),
  login: (credentials: Credentials) => request<Profile>('/auth/login', 'POST', credentials),
  logout: () => request<void>('/auth/logout', 'POST'),

  getProfile: () => request<Profile>('/profile'),
  updateProfile: (today: string, profile: UpdateProfileRequest) => request<Profile>(`/profile?today=${today}`, 'PUT', profile),

  getTasks: () => request<Task[]>('/tasks'),
  createTask: (task: SaveTaskRequest) => request<Task>('/tasks', 'POST', task),
  updateTask: (id: string, task: SaveTaskRequest) => request<Task>(`/tasks/${id}`, 'PUT', task),
  setTaskCompleted: (id: string, isCompleted: boolean) => request<Task>(`/tasks/${id}/completed`, 'PUT', { isCompleted }),
  deleteTask: (id: string) => request<void>(`/tasks/${id}`, 'DELETE'),

  getDashboard: (date: string) => request<Dashboard>(`/dashboard?date=${date}`),
  getChart: (date: string, scale: ChartScale) => request<Chart>(`/biorhythms/chart?date=${date}&scale=${scale}`),

  getSchedule: (date: string, today: string) => request<Schedule>(`/schedules/${date}?today=${today}`),
  buildSchedule: (date: string, today: string) => request<Schedule>(`/schedules/${date}/build?today=${today}`, 'POST'),
  moveBlock: (date: string, today: string, blockId: string, start: string) =>
    request<Schedule>(`/schedules/${date}/blocks/${blockId}?today=${today}`, 'PATCH', { start }),
  unpinBlock: (date: string, today: string, blockId: string) =>
    request<Schedule>(`/schedules/${date}/blocks/${blockId}/pin?today=${today}`, 'DELETE'),
}
