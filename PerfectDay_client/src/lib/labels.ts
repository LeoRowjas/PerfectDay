import type { BiorhythmCycle, Chronotype, CyclePhase, TaskPriority, TaskType } from '../api/types'

export const TASK_TYPES: TaskType[] = ['intellectual', 'physical', 'creative', 'routine']

export const taskTypeLabel: Record<TaskType, string> = {
  intellectual: 'Интеллектуальная',
  physical: 'Физическая',
  creative: 'Творческая',
  routine: 'Рутинная',
}

export const taskTypeFilterLabel: Record<TaskType, string> = {
  intellectual: 'Интеллектуальные',
  physical: 'Физические',
  creative: 'Творческие',
  routine: 'Рутинные',
}

export const taskTypeColor: Record<TaskType, string> = {
  intellectual: 'var(--type-intellectual)',
  physical: 'var(--type-physical)',
  creative: 'var(--type-creative)',
  routine: 'var(--type-routine)',
}

export const taskTypeHint: Record<TaskType, string> = {
  intellectual: 'Анализ, учёба, отчёты. Встанет на пик интеллектуального цикла.',
  physical: 'Спорт и активность. Встанет на пик физического цикла.',
  creative: 'Общение, презентации, творчество. Встанет на пик эмоционального цикла.',
  routine: 'Письма и простые дела. Встанет на часы спада, чтобы не занимать пики.',
}

export const priorityLabel: Record<TaskPriority, string> = {
  low: 'Низкий',
  medium: 'Средний',
  high: 'Высокий',
}

export const cycleLabel: Record<BiorhythmCycle, string> = {
  physical: 'Физический',
  emotional: 'Эмоциональный',
  intellectual: 'Интеллектуальный',
}

export const cycleColor: Record<BiorhythmCycle, string> = {
  physical: 'var(--cycle-physical)',
  emotional: 'var(--cycle-emotional)',
  intellectual: 'var(--cycle-intellectual)',
}

export function cyclePeriodLabel(days: number): string {
  // 23 и 33 дня, но 28 дней.
  const lastDigit = days % 10
  return `Цикл: ${days} ${lastDigit >= 2 && lastDigit <= 4 ? 'дня' : 'дней'}`
}

export const chronotypeOptions: { value: Chronotype; title: string; hint: string }[] = [
  { value: 'lark', title: 'Жаворонок', hint: 'Пик ясности утром, с 8:00 до 11:00' },
  { value: 'dove', title: 'Голубь', hint: 'Пик ясности днём, с 10:00 до 12:30' },
  { value: 'owl', title: 'Сова', hint: 'Пики сдвинуты на 3–4 часа позже' },
]

export const chronotypeName: Record<Chronotype, string> = {
  lark: 'жаворонок',
  dove: 'голубь',
  owl: 'сова',
}

export const taskTypeShortLabel: Record<TaskType, string> = {
  intellectual: 'Интел.',
  physical: 'Физ.',
  creative: 'Творч.',
  routine: 'Рутин.',
}

export const cycleShortLabel: Record<BiorhythmCycle, string> = {
  physical: 'Физ.',
  emotional: 'Эмоц.',
  intellectual: 'Интел.',
}

/** Какой цикл задаёт энергию для задачи этого типа; у рутины своего цикла нет. */
export const taskTypeCycle: Record<TaskType, BiorhythmCycle | null> = {
  intellectual: 'intellectual',
  physical: 'physical',
  creative: 'emotional',
  routine: null,
}

/** Короткое название дела для списка «Лучшие часы». */
export const activityLabel: Record<TaskType, string> = {
  intellectual: 'умственная работа',
  physical: 'спорт, прогулка',
  creative: 'общение, встречи',
  routine: 'рутина',
}

export const phaseBadge: Record<CyclePhase, string> = {
  rising: 'подъём',
  falling: 'спад',
  peak: 'пик',
  trough: 'спад',
  critical: 'критический',
}

export const phaseText: Record<CyclePhase, string> = {
  rising: 'растёт',
  falling: 'снижается',
  peak: 'близко к пику',
  trough: 'около минимума',
  critical: 'пересекает ноль',
}

export const priorityLabelLower: Record<TaskPriority, string> = {
  low: 'низкий',
  medium: 'средний',
  high: 'высокий',
}

export function formatPercent(percent: number): string {
  if (percent > 0) return `+${percent}%`
  return percent < 0 ? `−${Math.abs(percent)}%` : '0%'
}

/** «1 задача», «3 задачи», «6 задач». */
export function pluralize(count: number, forms: [string, string, string]): string {
  const lastTwo = count % 100
  const last = count % 10
  if (lastTwo >= 11 && lastTwo <= 14) return `${count} ${forms[2]}`
  if (last === 1) return `${count} ${forms[0]}`
  if (last >= 2 && last <= 4) return `${count} ${forms[1]}`
  return `${count} ${forms[2]}`
}
