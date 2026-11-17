import { useEffect, useRef } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { QueryClient } from '@tanstack/react-query'
import { ApiError, api } from './client'
import { nowTime, todayIso } from '../lib/date'
import type {
  AddBreakRequest,
  AnalyticsPeriod,
  CalendarProvider,
  CalendarSettings,
  Calendars,
  ChartScale,
  Credentials,
  Profile,
  ReminderSettings,
  SaveTaskRequest,
  Schedule,
  Task,
  UpdateProfileRequest,
} from './types'

const NOTIFICATIONS_POLL_MS = 60_000

export const queryKeys = {
  profile: ['profile'] as const,
  tasks: ['tasks'] as const,
  dashboard: ['dashboard'] as const,
  chart: ['chart'] as const,
  schedule: ['schedule'] as const,
  forecast: ['forecast'] as const,
  preview: ['preview'] as const,
  notifications: ['notifications'] as const,
  calendars: ['calendars'] as const,
  analytics: ['analytics'] as const,
  externalProviders: ['externalProviders'] as const,
}

const SYNC_INTERVAL_MS = 15 * 60_000
const timeZone = () => Intl.DateTimeFormat().resolvedOptions().timeZone

/**
 * Меняет текущего пользователя и убирает данные предыдущего.
 * Запрос профиля остаётся в кеше: после clear() подписчики не узнали бы о новой сессии.
 */
export function setSession(client: QueryClient, profile: Profile | null) {
  client.removeQueries({ predicate: (query) => query.queryKey[0] !== queryKeys.profile[0] })
  client.setQueryData(queryKeys.profile, profile)
}

/** Текущая сессия: null значит, что пользователь не вошёл. */
export function useProfile() {
  return useQuery<Profile | null>({
    queryKey: queryKeys.profile,
    queryFn: async () => {
      try {
        return await api.getProfile()
      } catch (error) {
        // 404: токен ещё действует, но аккаунта уже нет, например после очистки базы.
        if (error instanceof ApiError && (error.status === 401 || error.status === 404)) return null
        throw error
      }
    },
  })
}

function useAuthMutation(action: (credentials: Credentials) => Promise<Profile>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: action,
    onSuccess: (profile) => setSession(client, profile),
  })
}

export const useRegister = () => useAuthMutation(api.register)
export const useLogin = () => useAuthMutation(api.login)

export function useLogout() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: api.logout,
    onSuccess: () => setSession(client, null),
  })
}

export function useUpdateProfile() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (profile: UpdateProfileRequest) => api.updateProfile(todayIso(), profile),
    onSuccess: (profile) => {
      client.setQueryData(queryKeys.profile, profile)
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
      void client.invalidateQueries({ queryKey: queryKeys.chart })
      void client.invalidateQueries({ queryKey: queryKeys.schedule })
      void client.invalidateQueries({ queryKey: queryKeys.forecast })
    },
  })
}

export function useUpdateReminders() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (settings: ReminderSettings) => api.updateReminders(settings),
    onMutate: (settings) => {
      const previous = client.getQueryData<Profile | null>(queryKeys.profile)
      if (previous) client.setQueryData(queryKeys.profile, { ...previous, reminders: settings })
      return { previous }
    },
    onError: (_error, _settings, context) => client.setQueryData(queryKeys.profile, context?.previous),
    onSuccess: (profile) => {
      client.setQueryData(queryKeys.profile, profile)
      void client.invalidateQueries({ queryKey: queryKeys.notifications })
    },
  })
}

export function useTasks() {
  return useQuery({ queryKey: queryKeys.tasks, queryFn: () => api.getTasks(todayIso()) })
}

export function useSaveTask() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, task }: { id?: string; task: SaveTaskRequest }) =>
      id ? api.updateTask(todayIso(), id, task) : api.createTask(todayIso(), task),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.tasks })
      void client.invalidateQueries({ queryKey: queryKeys.schedule })
    },
  })
}

export function useSetTaskCompleted() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, isCompleted }: { id: string; isCompleted: boolean }) => api.setTaskCompleted(id, isCompleted),
    // Галочка меняется сразу, не дожидаясь ответа сервера; при ошибке значение вернётся обратно.
    onMutate: async ({ id, isCompleted }) => {
      await client.cancelQueries({ queryKey: queryKeys.tasks })
      const previous = client.getQueryData<Task[]>(queryKeys.tasks)
      client.setQueryData<Task[]>(queryKeys.tasks, (tasks) =>
        tasks?.map((task) => (task.id === id ? { ...task, isCompleted } : task)),
      )
      return { previous }
    },
    onError: (_error, _variables, context) => client.setQueryData(queryKeys.tasks, context?.previous),
    onSettled: () => {
      void client.invalidateQueries({ queryKey: queryKeys.tasks })
      void client.invalidateQueries({ queryKey: queryKeys.schedule })
    },
  })
}

/** Перенос задачи на другой день: из старого расписания она уходит, в новом встанет сама. */
export function useSetTaskDay() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, dueDate }: { id: string; dueDate: string }) => api.setTaskDay(id, dueDate),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.tasks })
      void client.invalidateQueries({ queryKey: queryKeys.schedule })
    },
  })
}

export function useDeleteTask() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, withFollowing }: { id: string; withFollowing?: boolean }) => api.deleteTask(id, withFollowing),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.tasks })
      void client.invalidateQueries({ queryKey: queryKeys.schedule })
    },
  })
}

export function useDashboard(date: string) {
  return useQuery({ queryKey: [...queryKeys.dashboard, date], queryFn: () => api.getDashboard(date) })
}

export function useChart(date: string, scale: ChartScale, enabled: boolean) {
  return useQuery({
    queryKey: [...queryKeys.chart, date, scale],
    queryFn: () => api.getChart(date, scale),
    enabled,
    placeholderData: keepPreviousData,
  })
}

export function useForecast(date: string, days: number, enabled: boolean) {
  return useQuery({
    queryKey: [...queryKeys.forecast, date, days],
    queryFn: () => api.getForecast(date, days),
    enabled,
  })
}

export function useSchedule(date: string) {
  return useQuery({
    queryKey: [...queryKeys.schedule, date],
    queryFn: () => api.getSchedule(date, todayIso()),
    placeholderData: keepPreviousData,
  })
}

function useScheduleUpdate<TArgs>(action: (args: TArgs) => Promise<Schedule>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: action,
    onSuccess: (schedule) => {
      // Другие даты тоже меняются: от этого расписания зависит список неразмещённых задач.
      void client.invalidateQueries({ queryKey: queryKeys.schedule })
      void client.invalidateQueries({ queryKey: queryKeys.notifications })
      void client.invalidateQueries({ queryKey: queryKeys.dashboard })
      client.setQueryData([...queryKeys.schedule, schedule.date], schedule)
    },
  })
}

export const useBuildSchedule = () => useScheduleUpdate((date: string) => api.buildSchedule(date, todayIso()))

export const useMoveBlock = () =>
  useScheduleUpdate(({ date, blockId, start }: { date: string; blockId: string; start: string }) =>
    api.moveBlock(date, todayIso(), blockId, start),
  )

export const useUnpinBlock = () =>
  useScheduleUpdate(({ date, blockId }: { date: string; blockId: string }) => api.unpinBlock(date, todayIso(), blockId))

export const useRemoveBreak = () =>
  useScheduleUpdate(({ date, blockId }: { date: string; blockId: string }) => api.removeBreak(date, todayIso(), blockId))

export const useAddBreak = () =>
  useScheduleUpdate(({ date, pause }: { date: string; pause: AddBreakRequest }) => api.addBreak(date, todayIso(), pause))

export const useSetLightened = () =>
  useScheduleUpdate(({ date, isLightened }: { date: string; isLightened: boolean }) =>
    api.setLightened(date, todayIso(), isLightened),
  )

export const useDismissWarning = () =>
  useScheduleUpdate(({ date, warningId }: { date: string; warningId: string }) =>
    api.dismissWarning(date, todayIso(), warningId),
  )

/** Что будет при переносе блока. Запрос ничего не меняет на сервере, поэтому это query, а не mutation. */
export function useMovePreview(date: string, blockId: string | null, start: string | null) {
  return useQuery({
    queryKey: [...queryKeys.preview, date, blockId, start],
    queryFn: () => api.previewMove(date, todayIso(), blockId!, start!),
    enabled: Boolean(blockId && start),
    placeholderData: keepPreviousData,
    staleTime: 0,
  })
}

/** Напоминания вычисляет сервер по расписанию и текущему времени клиента, поэтому список обновляется раз в минуту. */
export function useNotifications(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.notifications,
    queryFn: () => api.getNotifications(todayIso(), nowTime()),
    enabled,
    refetchInterval: NOTIFICATIONS_POLL_MS,
    staleTime: 0,
  })
}

export function useMarkNotificationsRead() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: api.markNotificationsRead,
    onSuccess: () => void client.invalidateQueries({ queryKey: queryKeys.notifications }),
  })
}

/**
 * Расписание дня, в которое новые задачи попадают сами: пользователь не должен нажимать кнопку.
 * Один и тот же набор задач размещается один раз, поэтому задачи, которым не хватило места, не вызывают повторов.
 */
export function usePlannedSchedule(date: string) {
  const { data: profile } = useProfile()
  const schedule = useSchedule(date)
  const build = useBuildSchedule()
  const attempted = useRef(new Set<string>())
  const { mutate } = build

  const current = schedule.data?.date === date ? schedule.data : undefined
  const isProfileComplete = Boolean(profile?.birthDate && profile.chronotype)
  const waiting = current?.unplacedTasks.map((task) => `${task.id}:${task.durationMinutes}`).sort().join() ?? ''

  useEffect(() => {
    if (!waiting || !isProfileComplete || date < todayIso() || schedule.isFetching) return

    const key = `${date}|${waiting}`
    if (attempted.current.has(key)) return
    attempted.current.add(key)
    mutate(date)
  }, [waiting, isProfileComplete, date, schedule.isFetching, mutate])

  return { schedule, current, build, isProfileComplete }
}

export function useExternalProviders() {
  return useQuery({ queryKey: queryKeys.externalProviders, queryFn: api.getExternalProviders, staleTime: Infinity })
}

export function useAnalytics(period: AnalyticsPeriod) {
  return useQuery({
    queryKey: [...queryKeys.analytics, period],
    queryFn: () => api.getAnalytics(todayIso(), period),
    placeholderData: keepPreviousData,
  })
}

export function useApplyRecommendations() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => api.applyRecommendations(todayIso()),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.analytics })
      void client.invalidateQueries({ queryKey: queryKeys.schedule })
    },
  })
}

export function useCalendars() {
  return useQuery({ queryKey: queryKeys.calendars, queryFn: () => api.getCalendars(todayIso()) })
}

/** Календари меняют расписание и задачи (события с «#задача»), поэтому после изменения обновляется всё. */
function useCalendarUpdate<TArgs>(action: (args: TArgs) => Promise<Calendars>) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: action,
    onSuccess: (calendars) => {
      client.setQueryData(queryKeys.calendars, calendars)
      void client.invalidateQueries({ queryKey: queryKeys.schedule })
      void client.invalidateQueries({ queryKey: queryKeys.tasks })
      void client.invalidateQueries({ queryKey: queryKeys.notifications })
    },
  })
}

export const useConnectCalendar = () =>
  useCalendarUpdate(({ provider, url }: { provider: CalendarProvider; url: string }) =>
    api.connectCalendar(todayIso(), provider, url, timeZone()),
  )

export const useUpdateCalendar = () =>
  useCalendarUpdate(({ id, settings }: { id: string; settings: CalendarSettings }) => api.updateCalendar(todayIso(), id, settings))

export const useSyncCalendars = () => useCalendarUpdate((force: boolean) => api.syncCalendars(todayIso(), timeZone(), force))

export const useDisconnectCalendar = () => useCalendarUpdate((id: string) => api.disconnectCalendar(todayIso(), id))

let lastBackgroundSync = 0

/**
 * Перед показом расписания подтягивает свежие события календарей. Сервер сам пропускает недавно обновлённые,
 * а клиент не спрашивает чаще раза в 15 минут.
 */
export function useBackgroundCalendarSync(enabled: boolean) {
  const { mutate } = useSyncCalendars()
  useEffect(() => {
    if (!enabled || Date.now() - lastBackgroundSync < SYNC_INTERVAL_MS) return
    lastBackgroundSync = Date.now()
    mutate(false)
  }, [enabled, mutate])
}
