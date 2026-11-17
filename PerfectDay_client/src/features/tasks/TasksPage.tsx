import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useSchedule, useSetTaskCompleted, useTasks } from '../../api/queries'
import type { Task, TaskType } from '../../api/types'
import { NotificationBell } from '../../components/NotificationCenter'
import { Badge, Button, Card, EmptyState, FormError, Icon, IconButton, Loading, PageHeader, Toggle } from '../../components/ui'
import { formatDate, formatDayMonth, formatRelativeDay, todayIso } from '../../lib/date'
import { repeatSummary } from '../../lib/recurrence'
import { formatDuration } from '../../lib/duration'
import {
  pluralize,
  priorityLabel,
  priorityLabelLower,
  TASK_TYPES,
  taskTypeColor,
  taskTypeFilterLabel,
  taskTypeLabel,
  taskTypeShortLabel,
} from '../../lib/labels'
import { MOBILE_QUERY, useMediaQuery } from '../../lib/useMediaQuery'
import { DeleteTaskDialog } from './DeleteTaskDialog'
import { TaskFormModal } from './TaskFormModal'
import styles from './TasksPage.module.css'

type EditorState = { mode: 'closed' } | { mode: 'new' } | { mode: 'edit'; task: Task }

const HIDE_COMPLETED_KEY = 'tasks.hideCompleted'

function readHideCompleted(): boolean {
  try {
    return localStorage.getItem(HIDE_COMPLETED_KEY) === '1'
  } catch {
    return false
  }
}

function TaskBadges({ task, today, unplaced }: { task: Task; today: string; unplaced: Set<string> }) {
  if (task.isCompleted) return <Badge tone="success">выполнено</Badge>
  return (
    <>
      {unplaced.has(task.id) && <Badge tone="warning">не поместилась</Badge>}
      {task.dueDate < today && <Badge tone="primary">перенесена</Badge>}
    </>
  )
}

/** Значок повтора или календаря рядом с названием. */
function TaskOrigin({ task }: { task: Task }) {
  if (task.repeat) {
    return (
      <span className={styles.origin} title={`Повторяется: ${repeatSummary(task.repeat).toLowerCase()}`}>
        <Icon name="repeat" size={14} />
        <span className={styles.visuallyHidden}>Повторяется: {repeatSummary(task.repeat).toLowerCase()}</span>
      </span>
    )
  }
  if (task.isFromCalendar) {
    return (
      <span className={styles.origin} title="Создана из события календаря">
        <Icon name="calendar" size={14} />
        <span className={styles.visuallyHidden}>Из календаря</span>
      </span>
    )
  }
  return null
}

function dayLabel(task: Task, today: string): string {
  if (!task.isCompleted && task.dueDate < today) return `${formatDayMonth(task.dueDate)} → сегодня`
  return formatRelativeDay(task.dueDate, today)
}

export function TasksPage() {
  const today = todayIso()
  const isMobile = useMediaQuery(MOBILE_QUERY)
  const tasks = useTasks()
  const todaySchedule = useSchedule(today)
  const setCompleted = useSetTaskCompleted()
  const [filter, setFilter] = useState<TaskType | 'all'>('all')
  const [hideCompleted, setHideCompleted] = useState(readHideCompleted)
  const [editor, setEditor] = useState<EditorState>({ mode: 'closed' })
  const [deleting, setDeleting] = useState<Task | null>(null)
  const [saved, setSaved] = useState<Task | null>(null)

  const openNew = () => setEditor({ mode: 'new' })
  const close = () => setEditor({ mode: 'closed' })
  const changeHideCompleted = (value: boolean) => {
    setHideCompleted(value)
    try {
      localStorage.setItem(HIDE_COMPLETED_KEY, value ? '1' : '0')
    } catch {
      // Без хранилища настройка просто не запомнится.
    }
  }

  const all = tasks.data ?? []
  const unplaced = new Set(todaySchedule.data?.date === today ? todaySchedule.data.unplacedTasks.map((task) => task.id) : [])
  const countOf = (type: TaskType | 'all') => (type === 'all' ? all.length : all.filter((task) => task.type === type).length)
  const matching = all.filter((task) => (filter === 'all' || task.type === filter) && !(hideCompleted && task.isCompleted))
  // Выполненные уходят вниз, чтобы список показывал, что ещё осталось сделать.
  const visible = [...matching].sort((a, b) => Number(a.isCompleted) - Number(b.isCompleted))
  const completedCount = all.filter((task) => task.isCompleted).length
  const savedDate = saved && (saved.dueDate < today ? today : saved.dueDate)

  const toggle = (task: Task) => setCompleted.mutate({ id: task.id, isCompleted: !task.isCompleted })

  return (
    <>
      <PageHeader
        title="Задачи"
        subtitle={
          isMobile && all.length > 0
            ? `${pluralize(all.length, ['задача', 'задачи', 'задач'])}, ${pluralize(completedCount, ['выполнена', 'выполнены', 'выполнено'])}`
            : undefined
        }
        action={
          <>
            <NotificationBell />
            {!isMobile && (
              <Button icon="plus" onClick={openNew}>
                Новая задача
              </Button>
            )}
          </>
        }
      />

      <div className={styles.toolbar}>
        <div className={styles.filters} role="group" aria-label="Фильтр по типу">
          {(['all', ...TASK_TYPES] as const).map((type) => (
            <button
              key={type}
              type="button"
              aria-pressed={filter === type}
              className={`${styles.chip} ${filter === type ? styles.chipActive : ''}`}
              onClick={() => setFilter(type)}
            >
              {type === 'all' ? 'Все' : isMobile ? taskTypeShortLabel[type] : taskTypeFilterLabel[type]}
              {!isMobile && type === 'all' && ` · ${countOf('all')}`}
            </button>
          ))}
        </div>
        {!isMobile && <Toggle label="Скрыть выполненные" checked={hideCompleted} onChange={changeHideCompleted} />}
      </div>

      {saved && savedDate && (
        <p role="status" className={styles.notice}>
          «{saved.title}» будет в расписании на {formatDate(savedDate)}.{' '}
          <Link to={savedDate === today ? '/schedule' : `/schedule?date=${savedDate}`}>Открыть расписание</Link>
        </p>
      )}

      <Card className={isMobile ? styles.mobileCard : styles.card}>
        {tasks.isPending && <Loading />}
        <FormError>{tasks.error?.message ?? setCompleted.error?.message}</FormError>

        {tasks.data && visible.length === 0 && (
          <EmptyState
            title={all.length === 0 ? 'Задач пока нет' : 'Здесь пусто'}
            text={
              all.length === 0
                ? 'Добавьте задачу, укажите тип, день и длительность — планировщик сам подберёт время.'
                : 'Выберите другой тип, покажите выполненные или добавьте новую задачу.'
            }
            action={all.length === 0 ? <Button onClick={openNew}>Добавить первую задачу</Button> : undefined}
          />
        )}

        {visible.length > 0 && isMobile && (
          <ul className={styles.cards}>
            {visible.map((task) => (
              <li key={task.id} className={`${styles.taskCard} ${task.isCompleted ? styles.completed : ''}`}>
                <input
                  type="checkbox"
                  className={styles.checkbox}
                  aria-label={`Выполнена: ${task.title}`}
                  checked={task.isCompleted}
                  onChange={() => toggle(task)}
                />
                <button type="button" className={styles.cardBody} onClick={() => setEditor({ mode: 'edit', task })}>
                  <span className={styles.title}>
                    {task.title} <TaskOrigin task={task} />
                  </span>
                  <span className={styles.meta}>
                    <span className={styles.dot} style={{ background: taskTypeColor[task.type] }} />
                    {dayLabel(task, today).split(',')[0]} · {formatDuration(task.durationMinutes)} · {priorityLabelLower[task.priority]}
                  </span>
                  {!task.isCompleted && (unplaced.has(task.id) || task.dueDate < today) && (
                    <span className={styles.badges}>
                      <TaskBadges task={task} today={today} unplaced={unplaced} />
                    </span>
                  )}
                </button>
                <IconButton icon="more-vertical" label={`Изменить «${task.title}»`} className={styles.more} onClick={() => setEditor({ mode: 'edit', task })} />
              </li>
            ))}
          </ul>
        )}

        {visible.length > 0 && !isMobile && (
          <div className={styles.tableScroll}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th className={styles.checkColumn}>
                    <span className={styles.visuallyHidden}>Выполнена</span>
                  </th>
                  <th className={styles.titleColumn}>Задача</th>
                  <th>Тип</th>
                  <th>День</th>
                  <th>Длительность</th>
                  <th>Приоритет</th>
                  <th className={styles.actionsColumn}>
                    <span className={styles.visuallyHidden}>Действия</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {visible.map((task) => (
                  <tr
                    key={task.id}
                    className={`${task.isCompleted ? styles.completed : ''} ${deleting?.id === task.id ? styles.deleting : ''}`}
                  >
                    <td>
                      <input
                        type="checkbox"
                        className={styles.checkbox}
                        aria-label={`Выполнена: ${task.title}`}
                        checked={task.isCompleted}
                        onChange={() => toggle(task)}
                      />
                    </td>
                    <td>
                      <span className={styles.titleCell}>
                        <button type="button" className={styles.title} onClick={() => setEditor({ mode: 'edit', task })}>
                          {task.title}
                        </button>
                        <TaskOrigin task={task} />
                        <TaskBadges task={task} today={today} unplaced={unplaced} />
                      </span>
                    </td>
                    <td>
                      <span className={styles.type}>
                        <span className={styles.dot} style={{ background: taskTypeColor[task.type] }} />
                        {taskTypeLabel[task.type]}
                      </span>
                    </td>
                    <td>{dayLabel(task, today)}</td>
                    <td>{formatDuration(task.durationMinutes)}</td>
                    <td>{priorityLabel[task.priority]}</td>
                    <td>
                      <span className={styles.rowActions}>
                        <button type="button" className={styles.rowAction} aria-label={`Изменить «${task.title}»`} onClick={() => setEditor({ mode: 'edit', task })}>
                          <Icon name="pencil" size={16} />
                        </button>
                        <button
                          type="button"
                          className={`${styles.rowAction} ${styles.rowDelete}`}
                          aria-label={`Удалить «${task.title}»`}
                          onClick={() => setDeleting(task)}
                        >
                          <Icon name="trash" size={16} />
                        </button>
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {isMobile && (
        <button type="button" className={styles.fab} aria-label="Новая задача" onClick={openNew}>
          <Icon name="plus" size={24} />
        </button>
      )}

      {deleting && <DeleteTaskDialog task={deleting} onCancel={() => setDeleting(null)} onDeleted={() => setDeleting(null)} />}
      {editor.mode === 'new' && <TaskFormModal onClose={close} onSaved={setSaved} />}
      {editor.mode === 'edit' && <TaskFormModal key={editor.task.id} task={editor.task} onClose={close} onSaved={setSaved} />}
    </>
  )
}
