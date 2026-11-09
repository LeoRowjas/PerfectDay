import { useEffect, useRef, useState } from 'react'
import type { PointerEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useMoveBlock, usePlannedSchedule, useSetTaskCompleted, useTasks, useUnpinBlock } from '../../api/queries'
import type { Schedule, ScheduleBlock, Task } from '../../api/types'
import { Button, Card, EmptyState, Field, FormError, Loading, PageHeader } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { WarningList } from '../../components/WarningList'
import { addDays, formatDate, formatLongDate, isIsoDate, minutesToTime, timeToMinutes, todayIso } from '../../lib/date'
import { formatDuration } from '../../lib/duration'
import { TaskFormModal } from '../tasks/TaskFormModal'
import styles from './SchedulePage.module.css'

const HOUR_HEIGHT = 64
const MIN_BLOCK_HEIGHT = 30
const COMPACT_BLOCK_MINUTES = 50
const DEFAULT_FIRST_HOUR = 8
const DEFAULT_LAST_HOUR = 19
const SNAP_MINUTES = 15
const DRAG_THRESHOLD_PX = 4
const CLOCK_TICK_MS = 60_000

const toPixels = (minutes: number) => (minutes / 60) * HOUR_HEIGHT

function blockTitle(block: ScheduleBlock): string {
  return block.partsTotal > 1 ? `${block.title} · часть ${block.part} из ${block.partsTotal}` : block.title
}

function useCurrentMinutes(): number {
  const read = () => {
    const now = new Date()
    return now.getHours() * 60 + now.getMinutes()
  }
  const [minutes, setMinutes] = useState(read)

  useEffect(() => {
    const timer = window.setInterval(() => setMinutes(read()), CLOCK_TICK_MS)
    return () => window.clearInterval(timer)
  }, [])

  return minutes
}

interface DragState {
  id: string
  pointerStartY: number
  originMinutes: number
  minutes: number
  moved: boolean
}

function Timeline({
  schedule,
  selectedId,
  isToday,
  isEditable,
  isBusy,
  onSelect,
  onMove,
}: {
  schedule: Schedule
  selectedId: string | null
  isToday: boolean
  isEditable: boolean
  isBusy: boolean
  onSelect: (id: string) => void
  onMove: (blockId: string, start: string) => void
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

  const startDrag = (event: PointerEvent<HTMLButtonElement>, block: ScheduleBlock) => {
    // На сенсорных экранах жест остаётся прокруткой, там блок переносят через панель справа.
    // Пока идёт перестройка дня, у блоков меняются идентификаторы: перенос старого блока вернул бы 404.
    if (!isEditable || isBusy || block.isCompleted || event.pointerType !== 'mouse' || event.button !== 0) return
    event.currentTarget.setPointerCapture(event.pointerId)
    const minutes = timeToMinutes(block.start)
    setDrag({ id: block.id, pointerStartY: event.clientY, originMinutes: minutes, minutes, moved: false })
  }

  const continueDrag = (event: PointerEvent<HTMLButtonElement>, duration: number) => {
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
      if (drag.minutes !== drag.originMinutes) onMove(drag.id, minutesToTime(drag.minutes))
    }
    setDrag(null)
  }

  return (
    <div className={styles.timeline}>
      {hours.map((hour) => (
        <div key={hour} className={styles.hour} style={{ height: HOUR_HEIGHT }}>
          <span className={styles.hourLabel}>{String(hour).padStart(2, '0')}:00</span>
        </div>
      ))}
      <div className={styles.blocks}>
        <div className={styles.offHours} style={{ top: 0, height: toPixels(workStart - dayOffset) }} />
        <div className={styles.offHours} style={{ top: toPixels(workEnd - dayOffset), height: toPixels(dayEnd - workEnd) }} />

        {schedule.blocks.map((block) => {
          const start = timeToMinutes(block.start)
          const duration = timeToMinutes(block.end) - start
          const isDragged = drag?.id === block.id && drag.moved
          const shownStart = isDragged ? drag.minutes : start
          const typeClass = block.taskType ? styles[block.taskType] : styles.break
          const classes = [
            styles.block,
            typeClass,
            duration < COMPACT_BLOCK_MINUTES ? styles.blockCompact : '',
            selectedId === block.id ? styles.blockSelected : '',
            isDragged ? styles.blockDragging : '',
            block.isCompleted ? styles.blockDone : '',
            isEditable && !block.isCompleted ? styles.blockDraggable : '',
          ]

          return (
            <button
              key={block.id}
              type="button"
              aria-pressed={selectedId === block.id}
              className={classes.join(' ')}
              style={{
                top: toPixels(shownStart - dayOffset),
                height: Math.max(toPixels(duration) - 2, MIN_BLOCK_HEIGHT),
              }}
              onPointerDown={(event) => startDrag(event, block)}
              onPointerMove={(event) => continueDrag(event, duration)}
              onPointerUp={finishDrag}
              onPointerCancel={() => setDrag(null)}
              onClick={() => {
                if (suppressClick.current) {
                  suppressClick.current = false
                  return
                }
                onSelect(block.id)
              }}
            >
              <span className={styles.blockTitle}>{blockTitle(block)}</span>
              <span className={styles.blockTime}>
                {minutesToTime(shownStart)}–{minutesToTime(shownStart + duration)}
                {block.isPinned && ' · закреплено'}
                {block.isCompleted && ' · выполнено'}
              </span>
            </button>
          )
        })}

        {isToday && now >= dayOffset && now < dayEnd && (
          <div className={styles.now} style={{ top: toPixels(now - dayOffset) }} aria-label={`Сейчас ${minutesToTime(now)}`} />
        )}
      </div>
    </div>
  )
}

function BlockDetails({
  block,
  date,
  isEditable,
  isMoving,
  moveError,
  onMove,
  onEditTask,
}: {
  block: ScheduleBlock
  date: string
  isEditable: boolean
  isMoving: boolean
  moveError?: string
  onMove: (start: string) => void
  onEditTask?: () => void
}) {
  const unpin = useUnpinBlock()
  const setCompleted = useSetTaskCompleted()
  const [start, setStart] = useState(block.start)

  return (
    <div className={styles.details}>
      <h3 className={styles.detailsTitle}>{blockTitle(block)}</h3>
      <p className={styles.detailsTime}>
        {block.start}–{block.end}
      </p>
      <p className={styles.explanation}>{block.explanation}</p>

      {block.taskId && (
        <div className={styles.detailsActions}>
          <Button
            variant={block.isCompleted ? 'outline' : 'primary'}
            disabled={setCompleted.isPending}
            onClick={() => setCompleted.mutate({ id: block.taskId!, isCompleted: !block.isCompleted })}
          >
            {block.isCompleted ? 'Вернуть в работу' : 'Выполнено'}
          </Button>
          {onEditTask && (
            <Button variant="outline" onClick={onEditTask}>
              Изменить задачу
            </Button>
          )}
        </div>
      )}

      {isEditable && !block.isCompleted && (
        <>
          <form
            className={styles.moveForm}
            onSubmit={(event) => {
              event.preventDefault()
              onMove(start)
            }}
          >
            <Field label="Перенести на" className={styles.moveField}>
              <input type="time" required value={start} onChange={(event) => setStart(event.target.value)} />
            </Field>
            <Button type="submit" variant="outline" disabled={isMoving || start === block.start}>
              Перенести
            </Button>
          </form>
          <p className={uiStyles.muted}>Блок можно перетащить мышью. Остальные задачи перестроятся вокруг него.</p>
        </>
      )}

      {isEditable && block.isPinned && (
        <button type="button" className={styles.linkButton} onClick={() => unpin.mutate({ date, blockId: block.id })}>
          Открепить: пусть планировщик снова двигает этот блок
        </button>
      )}

      <FormError>{moveError ?? unpin.error?.message ?? setCompleted.error?.message}</FormError>
    </div>
  )
}

function LoadCard({ schedule, isPast }: { schedule: Schedule | undefined; isPast: boolean }) {
  const hasBlocks = (schedule?.blocks.length ?? 0) > 0
  const unplaced = schedule?.unplacedTasks ?? []

  return (
    <Card title="Нагрузка">
      {!hasBlocks && <p className={uiStyles.muted}>Предупреждения о перегрузке и советы по отдыху появятся, когда в дне будут задачи.</p>}
      {hasBlocks && schedule!.warnings.length === 0 && (
        <p className={uiStyles.muted}>Перегрузки нет: нагрузка распределена равномерно.</p>
      )}
      {schedule && schedule.warnings.length > 0 && <WarningList warnings={schedule.warnings} />}
      {!isPast && unplaced.length > 0 && (
        <div className={styles.unplaced}>
          <h3 className={styles.unplacedTitle}>Не поместилось в рабочие часы</h3>
          <ul className={styles.unplacedList}>
            {unplaced.map((task) => (
              <li key={task.id}>
                {task.title}: {formatDuration(task.durationMinutes)}, день {formatDate(task.dueDate)}
              </li>
            ))}
          </ul>
          <p className={uiStyles.muted}>Сократите длительность, перенесите задачу на другой день или расширьте рабочие часы в профиле.</p>
        </div>
      )}
    </Card>
  )
}

type Editor = { mode: 'closed' } | { mode: 'new' } | { mode: 'edit'; task: Task }

export function SchedulePage() {
  const today = todayIso()
  const [params, setParams] = useSearchParams()
  const dateParam = params.get('date')
  const date = isIsoDate(dateParam) ? dateParam : today

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [editor, setEditor] = useState<Editor>({ mode: 'closed' })
  const { schedule, current, build, isProfileComplete } = usePlannedSchedule(date)
  const move = useMoveBlock()
  const tasks = useTasks()

  const selected = current?.blocks.find((block) => block.id === selectedId)
  const selectedTask = tasks.data?.find((task) => task.id === selected?.taskId)
  const hasBlocks = (current?.blocks.length ?? 0) > 0
  const isPast = date < today
  const isEditable = !isPast && isProfileComplete
  const isBusy = move.isPending || build.isPending

  const goTo = (next: string) => {
    setSelectedId(null)
    build.reset()
    move.reset()
    setParams(next === today ? {} : { date: next })
  }

  const moveBlock = (blockId: string, start: string) => move.mutate({ date, blockId, start })

  return (
    <>
      <PageHeader
        title="Расписание дня"
        action={
          isEditable ? (
            <div className={styles.headerActions}>
              <Button variant="outline" onClick={() => setEditor({ mode: 'new' })}>
                Новая задача
              </Button>
              {hasBlocks && (
                <Button disabled={isBusy} onClick={() => build.mutate(date)}>
                  Оптимизировать день
                </Button>
              )}
            </div>
          ) : undefined
        }
      />

      <div className={styles.dateBar}>
        <div className={uiStyles.segmented} role="group" aria-label="Выбор даты">
          <button type="button" className={uiStyles.segment} aria-label="Предыдущий день" onClick={() => goTo(addDays(date, -1))}>
            ←
          </button>
          <button
            type="button"
            aria-pressed={date === today}
            className={`${uiStyles.segment} ${date === today ? uiStyles.segmentActive : ''}`}
            onClick={() => goTo(today)}
          >
            Сегодня
          </button>
          <button type="button" className={uiStyles.segment} aria-label="Следующий день" onClick={() => goTo(addDays(date, 1))}>
            →
          </button>
        </div>
        <span className={uiStyles.muted}>
          {formatLongDate(date)}
          {isPast && ' · прошедший день, только просмотр'}
        </span>
      </div>

      {!isProfileComplete && (
        <p className={styles.notice}>
          Чтобы задачи встали в расписание, <Link to="/profile">укажите дату рождения и хронотип</Link>.
        </p>
      )}
      <FormError>{build.error?.message ?? schedule.error?.message}</FormError>

      <div className={styles.grid}>
        <Card title="Шкала дня">
          {!current && schedule.isPending && <Loading />}
          {current && (
            <Timeline
              schedule={current}
              selectedId={selectedId}
              isToday={date === today}
              isEditable={isEditable}
              isBusy={isBusy}
              onSelect={(id) => {
                move.reset()
                setSelectedId(id)
              }}
              onMove={moveBlock}
            />
          )}
          {current && !hasBlocks && current.unplacedTasks.length === 0 && !isPast && (
            <p className={uiStyles.muted}>На этот день задач нет. Добавьте задачу, и она сама встанет на подходящее время.</p>
          )}
        </Card>

        <div className={styles.side}>
          <Card title="Почему здесь?" className={styles.why}>
            {selected ? (
              <BlockDetails
                key={`${selected.id}-${selected.start}`}
                block={selected}
                date={date}
                isEditable={isEditable}
                isMoving={isBusy}
                moveError={move.error?.message}
                onMove={(start) => moveBlock(selected.id, start)}
                onEditTask={selectedTask ? () => setEditor({ mode: 'edit', task: selectedTask }) : undefined}
              />
            ) : (
              <>
                <EmptyState title="Выберите блок" text="Здесь будет объяснение, почему задача стоит в это время." />
                <FormError>{move.error?.message}</FormError>
              </>
            )}
          </Card>
          <LoadCard schedule={current} isPast={isPast} />
        </div>
      </div>

      {editor.mode === 'new' && <TaskFormModal defaultDate={date} onClose={() => setEditor({ mode: 'closed' })} />}
      {editor.mode === 'edit' && (
        <TaskFormModal key={editor.task.id} task={editor.task} onClose={() => setEditor({ mode: 'closed' })} />
      )}
    </>
  )
}
