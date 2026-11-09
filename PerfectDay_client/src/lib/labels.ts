import type { BiorhythmCycle, Chronotype, TaskPriority, TaskType } from '../api/types'

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
  { value: 'lark', title: 'Жаворонок', hint: 'Пик ясности — утром' },
  { value: 'dove', title: 'Голубь', hint: 'Пик ясности — днём' },
  { value: 'owl', title: 'Сова', hint: 'Пики сдвинуты на 3–4 часа позже' },
]
