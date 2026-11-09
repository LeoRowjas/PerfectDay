export type Chronotype = 'lark' | 'dove' | 'owl'
export type TaskType = 'intellectual' | 'physical' | 'creative' | 'routine'
export type TaskPriority = 'low' | 'medium' | 'high'
export type BiorhythmCycle = 'physical' | 'emotional' | 'intellectual'
export type BlockKind = 'task' | 'break'
export type WarningSeverity = 'info' | 'warning' | 'critical'
export type ChartScale = 'day' | 'week' | 'month'

export interface Profile {
  id: string
  email: string
  birthDate: string | null
  chronotype: Chronotype | null
  workdayStart: string
  workdayEnd: string
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
}

export type SaveTaskRequest = Omit<Task, 'id' | 'isCompleted'>

export interface CycleValue {
  cycle: BiorhythmCycle
  periodDays: number
  percent: number
  isCritical: boolean
}

export interface Recommendation {
  activity: TaskType
  title: string
  start: string
  end: string
  text: string
}

export interface LoadWarning {
  kind: string
  severity: WarningSeverity
  message: string
}

export interface Dashboard {
  date: string
  isProfileComplete: boolean
  cycles: CycleValue[]
  recommendations: Recommendation[]
  warnings: LoadWarning[]
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
  start: string
  end: string
  explanation: string
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

export interface Schedule {
  date: string
  isBuilt: boolean
  workdayStart: string
  workdayEnd: string
  blocks: ScheduleBlock[]
  unplacedTasks: UnplacedTask[]
  warnings: LoadWarning[]
}
