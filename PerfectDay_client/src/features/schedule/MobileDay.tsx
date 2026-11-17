import type { Schedule, ScheduleBlock } from '../../api/types'
import { Badge, Icon } from '../../components/ui'
import { timeToMinutes } from '../../lib/date'
import { formatDuration } from '../../lib/duration'
import { useCurrentMinutes } from '../../lib/useCurrentMinutes'
import { taskTypeLabel } from '../../lib/labels'
import { blockMinutes, blockTitle } from './scheduleUtils'
import styles from './SchedulePage.module.css'

/** На телефоне шкала заменена списком: перетаскивать на сенсорном экране неудобно, время меняют в панели блока. */
export function MobileDay({
  schedule,
  selectedId,
  isToday,
  onSelect,
  onToggleDone,
  renderDetails,
}: {
  schedule: Schedule
  selectedId: string | null
  isToday: boolean
  onSelect: (id: string) => void
  onToggleDone: (block: ScheduleBlock) => void
  renderDetails: (block: ScheduleBlock) => React.ReactNode
}) {
  const now = useCurrentMinutes()
  const flagged = new Set(schedule.warnings.flatMap((warning) => (warning.action?.taskId ? [warning.action.taskId] : [])))

  return (
    <ol className={styles.mobileList}>
      {schedule.blocks.map((block) => {
        const isNow = isToday && timeToMinutes(block.start) <= now && now < timeToMinutes(block.end)
        const isSelected = selectedId === block.id
        const typeClass = block.taskType ? styles[block.taskType] : styles.break

        return (
          <li key={block.id} className={styles.mobileRow}>
            <span className={styles.mobileTime}>{block.start}</span>
            <div className={styles.mobileCell}>
              {block.kind === 'task' ? (
                <div
                  role="button"
                  tabIndex={0}
                  aria-expanded={isSelected}
                  className={`${styles.mobileBlock} ${typeClass} ${isSelected ? styles.blockSelected : ''} ${block.isCompleted ? styles.blockDone : ''}`}
                  onClick={() => onSelect(isSelected ? '' : block.id)}
                  onKeyDown={(event) => event.key === 'Enter' && onSelect(isSelected ? '' : block.id)}
                >
                  <input
                    type="checkbox"
                    className={styles.blockCheck}
                    checked={block.isCompleted}
                    aria-label={`Выполнено: ${block.title}`}
                    onClick={(event) => event.stopPropagation()}
                    onChange={() => onToggleDone(block)}
                  />
                  <span className={styles.blockText}>
                    <span className={styles.blockTitle}>{blockTitle(block)}</span>
                    <span className={styles.blockTime}>
                      {block.taskType && taskTypeLabel[block.taskType]} · {formatDuration(blockMinutes(block))}
                    </span>
                  </span>
                  {isNow && <Badge tone="danger">сейчас</Badge>}
                  {!isNow && block.taskId && flagged.has(block.taskId) && (
                    <Icon name="alert-triangle" size={16} className={styles.mobileFlag} />
                  )}
                </div>
              ) : (
                <button
                  type="button"
                  className={`${styles.mobileBreak} ${block.kind === 'event' ? styles.mobileEvent : ''}`}
                  onClick={() => onSelect(isSelected ? '' : block.id)}
                >
                  <Icon name={block.kind === 'event' ? 'calendar' : block.title === 'Прогулка' ? 'footprints' : 'coffee'} size={14} />
                  <strong>{block.title}</strong> · {block.kind === 'event' ? `до ${block.end}` : formatDuration(blockMinutes(block))}
                </button>
              )}
              {isSelected && <div className={styles.mobileDetails}>{renderDetails(block)}</div>}
            </div>
          </li>
        )
      })}
    </ol>
  )
}
