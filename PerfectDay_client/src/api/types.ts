export type Chronotype = 'lark' | 'dove' | 'owl'
export type TaskType = 'intellectual' | 'physical' | 'creative' | 'routine'
export type TaskPriority = 'low' | 'medium' | 'high'
export type BiorhythmCycle = 'physical' | 'emotional' | 'intellectual'
export type BlockKind = 'task' | 'break' | 'event'
export type WarningSeverity = 'info' | 'warning' | 'critical'
export type ChartScale = 'day' | 'week' | 'month'
export type CyclePhase = 'rising' | 'falling' | 'peak' | 'trough' | 'critical'
export type LoadActionKind = 'addBreak' | 'insertWalk' | 'moveTask' | 'lighten'
export type BreakKind = 'rest' | 'walk'
export type NotificationKind = 'planReady' | 'taskStart' | 'breakSoon' | 'criticalTomorrow'
export type ExternalProvider = 'yandex' | 'vk' | 'google'
export type CalendarProvider = 'google' | 'yandex' | 'apple'
export type RecurrenceKind = 'daily' | 'weekly'
export type Weekday = 'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday' | 'sunday'
export type AnalyticsPeriod = 'week' | 'month'

export interface ReminderSettings {
  beforeTask: boolean
  rest: boolean
  browser: boolean
}

export interface Profile {
  id: string
  email: string
  birthDate: string | null
  chronotype: Chronotype | null
  workdayStart: string
  workdayEnd: string
  createdAt: string
  reminders: ReminderSettings
  hasPassword: boolean
  externalLogins: ExternalProvider[]
}

export interface ExternalProviderStatus {
  provider: ExternalProvider
  isEnabled: boolean
}

export interface UpdateProfileRequest {
  birthDate: string
  chronotype: Chronotype
  workdayStart: string
  workdayEnd: string
}

export interface Credentials {
  email: string
  password: string
}

export interface Task {
  id: string
  title: string
  type: TaskType
  dueDate: string
  durationMinutes: number
  priority: TaskPriority
  isCompleted: boolean
  seriesId: string | null
  repeat: Repeat | null
  isFromCalendar: boolean
}

export interface Repeat {
  kind: RecurrenceKind
  days: Weekday[] | null
  until: string | null
}

export type SaveTaskRequest = Omit<Task, 'id' | 'isCompleted' | 'seriesId' | 'isFromCalendar' | 'repeat'> & { repeat?: Repeat | null }

export interface CycleValue {
  cycle: BiorhythmCycle
  periodDays: number
  dayOfCycle: number
  percent: number
  phase: CyclePhase
  isCritical: boolean
}

export interface Recommendation {
  activity: TaskType
  title: string
  start: string
  end: string
  text: string
  energyFrom: number
  energyTo: number
  isDeclining: boolean
}

export interface LoadAction {
  kind: LoadActionKind
  start: string | null
  durationMinutes: number | null
  taskId: string | null
  targetDate: string | null
}

export interface LoadWarning {
  id: string
  kind: 'criticalDay' | 'longHardSeries' | 'tooMuchHardWork' | 'taskOnCycleDecline' | 'switchActivity'
  severity: WarningSeverity
  title: string
  message: string
  action: LoadAction | null
}

export interface Dashboard {
  date: string
  isProfileComplete: boolean
  isFirstVisit: boolean
  chronotype: Chronotype | null
  daysLived: number | null
  cycles: CycleValue[]
  recommendations: Recommendation[]
  warnings: LoadWarning[]
}

export interface GoodPeriod {
  cycle: BiorhythmCycle
  from: string
  to: string
  maxPercent: number
}

export interface CriticalDate {
  date: string
  cycles: BiorhythmCycle[]
}

export interface Forecast {
  from: string
  to: string
  goodPeriods: GoodPeriod[]
  criticalDates: CriticalDate[]
}

export interface ChartPoint {
  at: string
  physical: number
  emotional: number
  intellectual: number
  isCritical: boolean
}

export interface Chart {
  scale: ChartScale
  points: ChartPoint[]
}

export interface ScheduleBlock {
  id: string
  taskId: string | null
  kind: BlockKind
  title: string
  taskType: TaskType | null
  priority: TaskPriority | null
  start: string
  end: string
  explanation: string
  energyPercent: number | null
  isPinned: boolean
  isCompleted: boolean
  part: number
  partsTotal: number
}

export interface UnplacedTask {
  id: string
  title: string
  type: TaskType
  dueDate: string
  durationMinutes: number
}

export interface LoadSummary {
  hardMinutes: number
  workdayMinutes: number
  longestSeriesMinutes: number
  longestSeriesCount: number
  restMinutes: number
  isOverloaded: boolean
}

export interface Schedule {
  date: string
  isBuilt: boolean
  isLightened: boolean
  workdayStart: string
  workdayEnd: string
  blocks: ScheduleBlock[]
  unplacedTasks: UnplacedTask[]
  warnings: LoadWarning[]
  load: LoadSummary
}

export interface BlockChange {
  title: string
  from: string
  to: string
}

export interface MovePreview {
  start: string
  end: string
  energyPercent: number | null
  previousEnergyPercent: number | null
  warning: string | null
  changes: BlockChange[]
}

export interface AddBreakRequest {
  start: string
  durationMinutes: number
  kind: BreakKind
}

export interface AppNotification {
  id: string
  kind: NotificationKind
  title: string
  text: string
  at: string
  isRead: boolean
  isUpcoming: boolean
}

export interface Notifications {
  items: AppNotification[]
  unreadCount: number
}

export interface DayStat {
  date: string
  planned: number
  done: number
  isCritical: boolean
}

export interface TypeStat {
  type: TaskType
  planned: number
  done: number
  percent: number
}

export interface Insight {
  kind: 'timeOfDay' | 'postponed' | 'criticalDays' | 'trend'
  severity: WarningSeverity
  title: string
  text: string
}

export interface Analytics {
  period: AnalyticsPeriod
  from: string
  to: string
  planned: number
  done: number
  percent: number
  previousPercent: number | null
  hardDone: number
  peakPercent: number | null
  movedManually: number
  mostMovedType: TaskType | null
  criticalPlanned: number
  criticalDone: number
  criticalPercent: number | null
  days: DayStat[]
  types: TypeStat[]
  insights: Insight[]
  upcomingCriticalDates: string[]
  lightenedDates: string[]
}

export interface CalendarConnection {
  id: string
  provider: CalendarProvider
  name: string | null
  urlHint: string
  importAsBusy: boolean
  createTasksFromTagged: boolean
  lastSyncedAt: string | null
  lastError: string | null
}

export interface ImportedEvent {
  title: string
  provider: CalendarProvider
  start: string | null
  end: string | null
  isAllDay: boolean
  effect: 'busy' | 'outside' | 'task' | 'ignored'
  note: string
}

export interface Calendars {
  connections: CalendarConnection[]
  date: string
  events: ImportedEvent[]
  advice: { title: string; text: string; start: string; end: string } | null
}

export interface CalendarSettings {
  importAsBusy: boolean
  createTasksFromTagged: boolean
}
