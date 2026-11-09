import { useEffect, useRef } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { QueryClient } from '@tanstack/react-query'
import { ApiError, api } from './client'
import { todayIso } from '../lib/date'
import type { ChartScale, Credentials, Profile, SaveTaskRequest, Schedule, Task, UpdateProfileRequest } from './types'

export const queryKeys = {
  profile: ['profile'] as const,
  tasks: ['tasks'] as const,
  dashboard: ['dashboard'] as const,
  chart: ['chart'] as const,
  schedule: ['schedule'] as const,
}

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
    },
  })
}

export function useTasks() {
  return useQuery({ queryKey: queryKeys.tasks, queryFn: api.getTasks })
}

export function useSaveTask() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, task }: { id?: string; task: SaveTaskRequest }) =>
      id ? api.updateTask(id, task) : api.createTask(task),
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

export function useDeleteTask() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: api.deleteTask,
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
