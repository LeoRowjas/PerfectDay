import { useRef, useState } from 'react'
import type { PointerEvent } from 'react'
import type { LoadAction, Schedule, ScheduleBlock } from '../../api/types'
import { Icon } from '../../components/ui'
import { minutesToTime, timeToMinutes } from '../../lib/date'
import { useCurrentMinutes } from '../../lib/useCurrentMinutes'
import { taskTypeLabel } from '../../lib/labels'
import { blockTitle, suggestedPauses } from './scheduleUtils'
import type { MoveDraft } from './scheduleUtils'
import styles from './SchedulePage.module.css'

const HOUR_HEIGHT = 64
const MIN_BLOCK_HEIGHT = 28
const COMPACT_BLOCK_MINUTES = 50
const DEFAULT_FIRST_HOUR = 8
const DEFAULT_LAST_HOUR = 19
const SNAP_MINUTES = 15
const DRAG_THRESHOLD_PX = 4

const toPixels = (minutes: number) => (minutes / 60) * HOUR_HEIGHT

interface DragState {
  id: string
  pointerStartY: number
  originMinutes: number
  minutes: number
  moved: boolean
}

export function Timeline({
  schedule,
  selectedId,
  draft,
  isToday,
  isEditable,
  isBusy,
  onSelect,
  onDraft,
  onToggleDone,
  onSuggest,
}: {
  schedule: Schedule
  selectedId: string | null
  draft: MoveDraft | null
  isToday: boolean
  isEditable: boolean
  isBusy: boolean
  onSelect: (id: string) => void
  onDraft: (draft: MoveDraft) => void
  onToggleDone: (block: ScheduleBlock) => void
  onSuggest: (action: LoadAction & { start: string; durationMinutes: number }) => void
}) {
  const workStart = timeToMinutes(schedule.workdayStart)
  const workEnd = timeToMinutes(schedule.workdayEnd)
  const firstHour = Math.min(DEFAULT_FIRST_HOUR, Math.floor(workStart / 60))
  const lastHour = Math.max(DEFAULT_LAST_HOUR, Math.ceil(workEnd / 60))
  const hours = Array.from({ length: lastHour - firstHour + 1 }, (_, index) => firstHour + index)
  const dayOffset = firstHour * 60
  const dayEnd = (lastHour + 1) * 60

  const now = useCurrentMinutes()
  const [drag, setDrag] = useState<DragState | null>(null)
  const suppressClick = useRef(false)

  const startDrag = (event: PointerEvent<HTMLDivElement>, block: ScheduleBlock) => {
    // На сенсорных экранах жест остаётся прокруткой, там время задают в панели справа.
    // Пока идёт перестройка дня, у блоков меняются идентификаторы: перенос старого блока вернул бы 404.
    if (!isEditable || isBusy || block.isCompleted || block.kind === 'event' || event.pointerType !== 'mouse' || event.button !== 0) return
    if ((event.target as HTMLElement).closest('input')) return
    event.currentTarget.setPointerCapture(event.pointerId)
    const minutes = timeToMinutes(block.start)
    setDrag({ id: block.id, pointerStartY: event.clientY, originMinutes: minutes, minutes, moved: false })
  }

  const continueDrag = (event: PointerEvent<HTMLDivElement>, duration: number) => {
    if (!drag) return
    const offset = event.clientY - drag.pointerStartY
    if (!drag.moved && Math.abs(offset) < DRAG_THRESHOLD_PX) return

    const raw = drag.originMinutes + (offset / HOUR_HEIGHT) * 60
    const snapped = Math.round(raw / SNAP_MINUTES) * SNAP_MINUTES
    const minutes = Math.min(Math.max(snapped, workStart), workEnd - duration)
    setDrag({ ...drag, minutes, moved: true })
  }

  const finishDrag = () => {
    if (!drag) return
    if (drag.moved) {
      suppressClick.current = true
      onSelect(drag.id)
      if (drag.minutes !== drag.originMinutes) onDraft({ blockId: drag.id, start: minutesToTime(drag.minutes) })
    }
    setDrag(null)
  }

  return (
    <div className={styles.timeline}>
      {hours.map((hour) => (
        <div
          key={hour}
          className={`${styles.hour} ${hour * 60 < workStart || hour * 60 >= workEnd ? styles.hourOff : ''}`}
          style={{ height: HOUR_HEIGHT }}
        >
          <span className={styles.hourLabel}>{String(hour).padStart(2, '0')}:00</span>
        </div>
      ))}
      <div className={styles.blocks}>
        {schedule.blocks.map((block) => {
          const start = timeToMinutes(block.start)
          const duration = timeToMinutes(block.end) - start
          const isDragged = drag?.id === block.id && drag.moved
          const isDrafted = draft?.blockId === block.id
          const shownStart = isDragged ? drag.minutes : isDrafted ? timeToMinutes(draft.start) : start
          const typeClass = block.taskType ? styles[block.taskType] : block.kind === 'event' ? styles.event : styles.break
          const isTask = block.kind === 'task'
          const isCompact = duration < COMPACT_BLOCK_MINUTES
          const classes = [
            styles.block,
            typeClass,
            isCompact ? styles.blockCompact : '',
            selectedId === block.id ? styles.blockSelected : '',
            isDragged || isDrafted ? styles.blockDragging : '',
            block.isCompleted ? styles.blockDone : '',
            isEditable && !block.isCompleted && block.kind !== 'event' ? styles.blockDraggable : '',
          ]

          return (
            <div
              key={block.id}
              role="button"
              tabIndex={0}
              aria-pressed={selectedId === block.id}
              className={classes.join(' ')}
              style={{
                top: toPixels(shownStart - dayOffset) + 1,
                height: Math.max(toPixels(duration) - 3, MIN_BLOCK_HEIGHT),
              }}
              onPointerDown={(event) => startDrag(event, block)}
              onPointerMove={(event) => continueDrag(event, duration)}
              onPointerUp={finishDrag}
              onPointerCancel={() => setDrag(null)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault()
                  onSelect(block.id)
                }
              }}
              onClick={() => {
                if (suppressClick.current) {
                  suppressClick.current = false
                  return
                }
                onSelect(block.id)
              }}
            >
              {isTask ? (
                <input
                  type="checkbox"
                  className={styles.blockCheck}
                  checked={block.isCompleted}
                  aria-label={`Выполнено: ${block.title}`}
                  onClick={(event) => event.stopPropagation()}
                  onChange={() => onToggleDone(block)}
                />
              ) : (
                <Icon name={block.kind === 'event' ? 'calendar' : block.title === 'Прогулка' ? 'footprints' : 'coffee'} size={14} />
              )}
              <span className={styles.blockText}>
                <span className={styles.blockTitle}>{blockTitle(block)}</span>
                <span className={styles.blockTime}>
                  {minutesToTime(shownStart)}–{minutesToTime(shownStart + duration)}
                  {block.taskType && ` · ${taskTypeLabel[block.taskType]}`}
                  {block.kind === 'event' && ' · из календаря'}
                </span>
              </span>
              {(block.isPinned || isDrafted) && isTask && <Icon name="pin" size={14} className={styles.blockPin} />}
            </div>
          )
        })}

        {isEditable &&
          !draft &&
          suggestedPauses(schedule).map((pause) => {
            const start = timeToMinutes(pause.start)
            const end = start + pause.durationMinutes
            return (
              <div key={pause.warningId} className={styles.suggestion} style={{ top: toPixels(start - dayOffset) - 1 }}>
                <button type="button" className={styles.suggestionPill} disabled={isBusy} onClick={() => onSuggest(pause)}>
                  <Icon name="plus" size={12} />
                  {pause.kind === 'insertWalk' ? 'Предлагаемая прогулка' : 'Предлагаемый перерыв'} {pause.start}–{minutesToTime(end)}
                </button>
              </div>
            )
          })}

        {isToday && now >= dayOffset && now < dayEnd && (
          <div className={styles.now} style={{ top: toPixels(now - dayOffset) }} aria-label={`Сейчас ${minutesToTime(now)}`} />
        )}
      </div>
    </div>
  )
}
