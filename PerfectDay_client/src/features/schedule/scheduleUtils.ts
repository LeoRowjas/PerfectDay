import type { LoadAction, Schedule, ScheduleBlock } from '../../api/types'
import { timeToMinutes } from '../../lib/date'

export interface MoveDraft {
  blockId: string
  start: string
}

export function blockTitle(block: ScheduleBlock): string {
  return block.partsTotal > 1 ? `${block.title} · часть ${block.part} из ${block.partsTotal}` : block.title
}

export const blockMinutes = (block: ScheduleBlock) => timeToMinutes(block.end) - timeToMinutes(block.start)

export function taskStats(schedule: Schedule): { count: number; minutes: number } {
  const tasks = schedule.blocks.filter((block) => block.kind === 'task')
  return { count: new Set(tasks.map((block) => block.taskId)).size, minutes: tasks.reduce((sum, block) => sum + blockMinutes(block), 0) }
}

/** Предложенные анализом паузы, которые можно показать прямо на шкале. */
export function suggestedPauses(schedule: Schedule): (LoadAction & { start: string; durationMinutes: number; warningId: string })[] {
  return schedule.warnings.flatMap((warning) => {
    const action = warning.action
    if (!action || (action.kind !== 'addBreak' && action.kind !== 'insertWalk') || !action.start || !action.durationMinutes) return []
    return [{ ...action, start: action.start, durationMinutes: action.durationMinutes, warningId: warning.id }]
  })
}
