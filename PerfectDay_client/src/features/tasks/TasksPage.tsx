import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useSetTaskCompleted, useTasks } from '../../api/queries'
import type { Task, TaskType } from '../../api/types'
import { Button, Card, EmptyState, FormError, Loading, PageHeader } from '../../components/ui'
import { formatDate, todayIso } from '../../lib/date'
import { formatDuration } from '../../lib/duration'
import { priorityLabel, TASK_TYPES, taskTypeColor, taskTypeFilterLabel, taskTypeLabel } from '../../lib/labels'
import { TaskFormModal } from './TaskFormModal'
import styles from './TasksPage.module.css'

type EditorState = { mode: 'closed' } | { mode: 'new' } | { mode: 'edit'; task: Task }

export function TasksPage() {
  const tasks = useTasks()
  const setCompleted = useSetTaskCompleted()
  const [filter, setFilter] = useState<TaskType | 'all'>('all')
  const [editor, setEditor] = useState<EditorState>({ mode: 'closed' })
  const [saved, setSaved] = useState<Task | null>(null)

  const openNew = () => setEditor({ mode: 'new' })
  const close = () => setEditor({ mode: 'closed' })

  const today = todayIso()
  const all = tasks.data ?? []
  const matching = filter === 'all' ? all : all.filter((task) => task.type === filter)
  // Выполненные уходят вниз, чтобы список показывал, что ещё осталось сделать.
  const visible = [...matching].sort((a, b) => Number(a.isCompleted) - Number(b.isCompleted))
  const savedDate = saved && (saved.dueDate < today ? today : saved.dueDate)

  return (
    <>
      <PageHeader title="Задачи" action={<Button onClick={openNew}>Новая задача</Button>} />

      <div className={styles.filters} role="group" aria-label="Фильтр по типу">
        {(['all', ...TASK_TYPES] as const).map((type) => (
          <button
            key={type}
            type="button"
            aria-pressed={filter === type}
            className={`${styles.chip} ${filter === type ? styles.chipActive : ''}`}
            onClick={() => setFilter(type)}
          >
            {type === 'all' ? 'Все' : taskTypeFilterLabel[type]}
          </button>
        ))}
      </div>

      {saved && savedDate && (
        <p role="status" className={styles.notice}>
          «{saved.title}» будет в расписании на {formatDate(savedDate)}.{' '}
          <Link to={savedDate === today ? '/schedule' : `/schedule?date=${savedDate}`}>Открыть расписание</Link>
        </p>
      )}

      <Card className={styles.card}>
        {tasks.isPending && <Loading />}
        <FormError>{tasks.error?.message ?? setCompleted.error?.message}</FormError>

        {tasks.data && visible.length === 0 && (
          <EmptyState
            title={all.length === 0 ? 'Задач пока нет' : 'Нет задач этого типа'}
            text={
              all.length === 0
                ? 'Добавьте задачу, укажите тип, день и длительность — планировщик сам подберёт время.'
                : 'Выберите другой тип или добавьте новую задачу.'
            }
            action={all.length === 0 ? <Button onClick={openNew}>Добавить первую задачу</Button> : undefined}
          />
        )}

        {visible.length > 0 && (
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
                </tr>
              </thead>
              <tbody>
                {visible.map((task) => (
                  <tr
                    key={task.id}
                    className={task.isCompleted ? styles.completed : undefined}
                    onClick={() => setEditor({ mode: 'edit', task })}
                  >
                    <td onClick={(event) => event.stopPropagation()}>
                      <input
                        type="checkbox"
                        className={styles.checkbox}
                        aria-label={`Выполнена: ${task.title}`}
                        checked={task.isCompleted}
                        onChange={(event) => setCompleted.mutate({ id: task.id, isCompleted: event.target.checked })}
                      />
                    </td>
                    <td>
                      <button
                        type="button"
                        className={styles.title}
                        onClick={(event) => {
                          event.stopPropagation()
                          setEditor({ mode: 'edit', task })
                        }}
                      >
                        {task.title}
                      </button>
                    </td>
                    <td>
                      <span className={styles.type}>
                        <span className={styles.dot} style={{ background: taskTypeColor[task.type] }} />
                        {taskTypeLabel[task.type]}
                      </span>
                    </td>
                    <td>
                      {formatDate(task.dueDate)}
                      {!task.isCompleted && task.dueDate < today && <span className={styles.overdue}>перенесена на сегодня</span>}
                    </td>
                    <td>{formatDuration(task.durationMinutes)}</td>
                    <td>{priorityLabel[task.priority]}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {editor.mode === 'new' && <TaskFormModal onClose={close} onSaved={setSaved} />}
      {editor.mode === 'edit' && <TaskFormModal key={editor.task.id} task={editor.task} onClose={close} onSaved={setSaved} />}
    </>
  )
}
