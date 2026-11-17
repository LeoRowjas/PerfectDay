import type {
  AddBreakRequest,
  Analytics,
  AnalyticsPeriod,
  CalendarProvider,
  Calendars,
  CalendarSettings,
  ExternalProviderStatus,
  Chart,
  ChartScale,
  Credentials,
  Dashboard,
  Forecast,
  MovePreview,
  Notifications,
  Profile,
  ReminderSettings,
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
  getExternalProviders: () => request<ExternalProviderStatus[]>('/auth/external/providers'),

  getProfile: () => request<Profile>('/profile'),
  updateProfile: (today: string, profile: UpdateProfileRequest) => request<Profile>(`/profile?today=${today}`, 'PUT', profile),
  updateReminders: (settings: ReminderSettings) => request<Profile>('/profile/reminders', 'PUT', settings),

  getTasks: (today: string) => request<Task[]>(`/tasks?today=${today}`),
  createTask: (today: string, task: SaveTaskRequest) => request<Task>(`/tasks?today=${today}`, 'POST', task),
  updateTask: (today: string, id: string, task: SaveTaskRequest) => request<Task>(`/tasks/${id}?today=${today}`, 'PUT', task),
  setTaskCompleted: (id: string, isCompleted: boolean) => request<Task>(`/tasks/${id}/completed`, 'PUT', { isCompleted }),
  setTaskDay: (id: string, dueDate: string) => request<Task>(`/tasks/${id}/day`, 'PUT', { dueDate }),
  deleteTask: (id: string, withFollowing = false) => request<void>(`/tasks/${id}?withFollowing=${withFollowing}`, 'DELETE'),

  getDashboard: (date: string) => request<Dashboard>(`/dashboard?date=${date}`),
  getChart: (date: string, scale: ChartScale) => request<Chart>(`/biorhythms/chart?date=${date}&scale=${scale}`),
  getForecast: (date: string, days: number) => request<Forecast>(`/biorhythms/forecast?date=${date}&days=${days}`),

  getSchedule: (date: string, today: string) => request<Schedule>(`/schedules/${date}?today=${today}`),
  buildSchedule: (date: string, today: string) => request<Schedule>(`/schedules/${date}/build?today=${today}`, 'POST'),
  moveBlock: (date: string, today: string, blockId: string, start: string) =>
    request<Schedule>(`/schedules/${date}/blocks/${blockId}?today=${today}`, 'PATCH', { start }),
  previewMove: (date: string, today: string, blockId: string, start: string) =>
    request<MovePreview>(`/schedules/${date}/blocks/${blockId}/preview?today=${today}`, 'POST', { start }),
  unpinBlock: (date: string, today: string, blockId: string) =>
    request<Schedule>(`/schedules/${date}/blocks/${blockId}/pin?today=${today}`, 'DELETE'),
  removeBreak: (date: string, today: string, blockId: string) =>
    request<Schedule>(`/schedules/${date}/blocks/${blockId}?today=${today}`, 'DELETE'),
  addBreak: (date: string, today: string, pause: AddBreakRequest) =>
    request<Schedule>(`/schedules/${date}/breaks?today=${today}`, 'POST', pause),
  setLightened: (date: string, today: string, isLightened: boolean) =>
    request<Schedule>(`/schedules/${date}/lightened?today=${today}`, 'PUT', { isLightened }),
  dismissWarning: (date: string, today: string, warningId: string) =>
    request<Schedule>(`/schedules/${date}/warnings/dismiss?today=${today}`, 'POST', { warningId }),

  getNotifications: (today: string, now: string) => request<Notifications>(`/notifications?today=${today}&now=${now}`),
  markNotificationsRead: (ids: string[]) => request<void>('/notifications/read', 'POST', { ids }),

  getCalendars: (today: string) => request<Calendars>(`/calendars?today=${today}`),
  connectCalendar: (today: string, provider: CalendarProvider, url: string, timeZone: string) =>
    request<Calendars>(`/calendars?today=${today}`, 'POST', { provider, url, timeZone }),
  updateCalendar: (today: string, id: string, settings: CalendarSettings) =>
    request<Calendars>(`/calendars/${id}?today=${today}`, 'PUT', settings),
  syncCalendars: (today: string, timeZone: string, force: boolean) =>
    request<Calendars>(`/calendars/sync?today=${today}`, 'POST', { timeZone, force }),
  disconnectCalendar: (today: string, id: string) => request<Calendars>(`/calendars/${id}?today=${today}`, 'DELETE'),

  getAnalytics: (today: string, period: AnalyticsPeriod) => request<Analytics>(`/analytics?today=${today}&period=${period}`),
  applyRecommendations: (today: string) => request<{ lightenedDates: string[] }>(`/analytics/apply?today=${today}`, 'POST'),
}

/** Отчёт скачивается обычной ссылкой: cookie сессии уходит вместе с запросом. */
export const analyticsReportUrl = (today: string, period: AnalyticsPeriod) => `/api/analytics/report?today=${today}&period=${period}`

/** Вход через внешний сервис — переход страницы, а не fetch: браузер уходит к провайдеру и возвращается. */
export const externalLoginUrl = (provider: string) => `/api/auth/external/${provider}`
