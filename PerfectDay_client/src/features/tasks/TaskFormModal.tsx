import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { useSaveTask } from '../../api/queries'
import type { Repeat, Task, TaskPriority, TaskType, Weekday } from '../../api/types'
import { Button, Field, FormError, Icon, Modal } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { todayIso } from '../../lib/date'
import { formatDuration, parseDuration } from '../../lib/duration'
import { cycleLabel, pluralize, priorityLabel, TASK_TYPES, taskTypeCycle, taskTypeHint, taskTypeLabel } from '../../lib/labels'
import { endOfMonth, formatDates, occurrences, WEEKDAYS } from '../../lib/recurrence'
import { DeleteTaskDialog } from './DeleteTaskDialog'
import styles from './TaskFormModal.module.css'

const MIN_DURATION = 5
const MAX_DURATION = 720
const PRIORITIES: TaskPriority[] = ['low', 'medium', 'high']
const DURATION_PRESETS = [30, 60, 90, 120]
const UPCOMING_COUNT = 3
const REPEAT_OPTIONS = [
  { value: 'none', label: 'Не повторять' },
  { value: 'daily', label: 'Каждый день' },
  { value: 'weekly', label: 'По дням недели' },
] as const
const WEEKDAY_BY_INDEX: Weekday[] = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday']

const schema = z.object({
  title: z.string().trim().min(1, 'Укажите название.').max(200, 'Не длиннее 200 символов.'),
  type: z.enum(['intellectual', 'physical', 'creative', 'routine'], { message: 'Выберите тип задачи.' }),
  dueDate: z.string().min(1, 'Укажите день.'),
  duration: z.string().refine((value) => {
    const minutes = parseDuration(value)
    return minutes !== null && minutes >= MIN_DURATION && minutes <= MAX_DURATION
  }, 'От 5 минут до 12 часов, например, 90 мин.'),
  priority: z.enum(['low', 'medium', 'high']),
  repeatKind: z.enum(['none', 'daily', 'weekly']),
  repeatDays: z.array(z.enum(['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'])),
  repeatUntil: z.string(),
})
  .refine((form) => form.repeatKind !== 'weekly' || form.repeatDays.length > 0, {
    message: 'Выберите хотя бы один день.',
    path: ['repeatDays'],
  })
  .refine((form) => form.repeatKind === 'none' || !form.repeatUntil || form.repeatUntil >= form.dueDate, {
    message: 'Не раньше первого дня.',
    path: ['repeatUntil'],
  })

type TaskForm = z.infer<typeof schema>

const weekdayOf = (iso: string): Weekday => WEEKDAY_BY_INDEX[new Date(`${iso}T00:00`).getDay()]

function toRepeat(form: Pick<TaskForm, 'repeatKind' | 'repeatDays' | 'repeatUntil'>): Repeat | null {
  if (form.repeatKind === 'none') return null
  return {
    kind: form.repeatKind,
    days: form.repeatKind === 'weekly' ? form.repeatDays : null,
    until: form.repeatUntil || null,
  }
}

function repeatHint(type: TaskType | undefined): string {
  const cycle = type && taskTypeCycle[type]
  if (!cycle) return 'Каждое повторение планировщик поставит на подходящее время своего дня, рутину — на часы спада.'
  return (
    `Каждое повторение планировщик поставит на лучшее время своего дня: в дни подъёма ${cycleLabel[cycle].toLowerCase().replace(/[иы]й$/, 'ого')} ` +
    'цикла — на пик, в критические дни оставит запас времени.'
  )
}

/** «Ближайшие: 11, 13, 16 ноября» и сколько повторений до конца месяца. */
function RepeatPreview({ repeat, start }: { repeat: Repeat; start: string }) {
  if (!start) return null
  const upcoming = occurrences(repeat, start, UPCOMING_COUNT)
  const thisMonth = occurrences(repeat, start, 100, endOfMonth(start)).length
  if (upcoming.length === 0) return <span className={uiStyles.error}>В этот период нет ни одного повторения.</span>

  return (
    <span className={styles.repeatPreview}>
      <strong>Ближайшие: {formatDates(upcoming)}</strong>
      <span className={uiStyles.muted}>{pluralize(thisMonth, ['повторение', 'повторения', 'повторений'])} до конца месяца</span>
    </span>
  )
}
export function TaskFormModal({
  task,
  defaultDate,
  onClose,
  onSaved,
}: {
  task?: Task
  defaultDate?: string
  onClose: () => void
  onSaved?: (task: Task) => void
}) {
  const save = useSaveTask()
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const form = useForm<TaskForm>({
    resolver: zodResolver(schema),
    defaultValues: task
      ? {
          title: task.title,
          type: task.type,
          dueDate: task.dueDate,
          duration: formatDuration(task.durationMinutes),
          priority: task.priority,
          repeatKind: task.repeat?.kind ?? 'none',
          repeatDays: task.repeat?.days ?? [weekdayOf(task.dueDate)],
          repeatUntil: task.repeat?.until ?? '',
        }
      : {
          title: '',
          dueDate: defaultDate ?? todayIso(),
          duration: '',
          priority: 'medium',
          repeatKind: 'none',
          repeatDays: [weekdayOf(defaultDate ?? todayIso())],
          repeatUntil: '',
        },
  })
  const { errors } = form.formState
  const selectedType = useWatch({ control: form.control, name: 'type' })
  const [dueDate, repeatKind, repeatDays, repeatUntil] = useWatch({
    control: form.control,
    name: ['dueDate', 'repeatKind', 'repeatDays', 'repeatUntil'],
  })
  const repeat = toRepeat({ repeatKind, repeatDays, repeatUntil })

  const onSubmit = ({ title, type, dueDate, duration, priority, ...rest }: TaskForm) => {
    save.mutate(
      { id: task?.id, task: { title, type, dueDate, durationMinutes: parseDuration(duration)!, priority, repeat: toRepeat(rest) } },
      {
        onSuccess: (saved) => {
          onSaved?.(saved)
          onClose()
        },
      },
    )
  }

  const isBusy = save.isPending
  const toggleDay = (day: Weekday) =>
    form.setValue('repeatDays', repeatDays.includes(day) ? repeatDays.filter((d) => d !== day) : [...repeatDays, day], {
      shouldValidate: true,
    })

  return (
    <Modal title={task ? 'Редактирование задачи' : 'Новая задача'} onClose={onClose}>
      <form className={styles.form} noValidate onSubmit={form.handleSubmit(onSubmit)}>
        <Field label="Название" error={errors.title?.message}>
          <input
            type="text"
            placeholder="Что нужно сделать?"
            aria-invalid={Boolean(errors.title)}
            {...form.register('title')}
          />
        </Field>

        <fieldset className={styles.types}>
          <legend className={uiStyles.fieldLabel}>Тип задачи</legend>
          <Controller
            control={form.control}
            name="type"
            render={({ field }) => (
              <div className={styles.typeOptions}>
                {TASK_TYPES.map((type) => (
                  <label key={type} className={`${styles.typeOption} ${field.value === type ? styles.typeSelected : ''}`}>
                    <input
                      type="radio"
                      name={field.name}
                      value={type}
                      checked={field.value === type}
                      onChange={() => field.onChange(type)}
                    />
                    {taskTypeLabel[type]}
                  </label>
                ))}
              </div>
            )}
          />
          {errors.type ? (
            <span className={uiStyles.error}>{errors.type.message}</span>
          ) : (
            <span className={uiStyles.muted}>
              {selectedType ? taskTypeHint[selectedType] : 'От типа зависит, на какое время встанет задача.'}
            </span>
          )}
        </fieldset>

        <div className={styles.row}>
          <Field label={repeat ? 'Начиная с' : 'День'} error={errors.dueDate?.message}>
            <input type="date" aria-invalid={Boolean(errors.dueDate)} {...form.register('dueDate')} />
          </Field>
          <Field label="Длительность" error={errors.duration?.message}>
            <input
              type="text"
              placeholder="например, 90 мин"
              aria-invalid={Boolean(errors.duration)}
              {...form.register('duration')}
            />
          </Field>
          <Field label="Приоритет">
            <select className={styles.select} {...form.register('priority')}>
              {PRIORITIES.map((priority) => (
                <option key={priority} value={priority}>
                  {priorityLabel[priority]}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className={styles.presets}>
          {DURATION_PRESETS.map((minutes) => (
            <button
              key={minutes}
              type="button"
              className={styles.preset}
              onClick={() => form.setValue('duration', formatDuration(minutes), { shouldValidate: true })}
            >
              {formatDuration(minutes)}
            </button>
          ))}
        </div>

        <section className={styles.repeat} aria-label="Повторять">
          <span className={styles.repeatTitle}>
            <Icon name="repeat" size={16} />
            Повторять
          </span>
          <div className={uiStyles.segmented} role="group" aria-label="Как повторять">
            {REPEAT_OPTIONS.map((option) => (
              <button
                key={option.value}
                type="button"
                aria-pressed={repeatKind === option.value}
                className={`${uiStyles.segment} ${repeatKind === option.value ? uiStyles.segmentActive : ''}`}
                onClick={() => {
                  // Еженедельный повтор по умолчанию — в тот же день недели, что и первый день.
                  if (option.value === 'weekly' && repeatKind !== 'weekly' && !task?.repeat) form.setValue('repeatDays', [weekdayOf(dueDate)])
                  form.setValue('repeatKind', option.value, { shouldValidate: true })
                }}
              >
                {option.label}
              </button>
            ))}
          </div>

          {repeatKind === 'weekly' && (
            <div className={styles.weekdays} role="group" aria-label="Дни недели">
              {WEEKDAYS.map((day) => (
                <button
                  key={day.value}
                  type="button"
                  aria-pressed={repeatDays.includes(day.value)}
                  className={`${styles.weekday} ${repeatDays.includes(day.value) ? styles.weekdaySelected : ''}`}
                  onClick={() => toggleDay(day.value)}
                >
                  {day.short}
                </button>
              ))}
            </div>
          )}
          {errors.repeatDays && <span className={uiStyles.error}>{errors.repeatDays.message}</span>}

          {repeat && (
            <div className={styles.until}>
              <Field label="До даты" error={errors.repeatUntil?.message}>
                <input type="date" aria-invalid={Boolean(errors.repeatUntil)} {...form.register('repeatUntil')} />
              </Field>
              <RepeatPreview repeat={repeat} start={dueDate} />
            </div>
          )}
        </section>

        <p className={`${uiStyles.muted} ${styles.hint}`}>
          {repeat && <Icon name="sparkles" size={14} />}
          {repeat
            ? repeatHint(selectedType)
            : 'В этот день задача сама встанет в расписание. Если её не выполнить, она перейдёт на следующий день.'}
        </p>
        {task?.repeat && (
          <p className={uiStyles.muted}>
            {repeat
              ? 'Изменения применятся к этой задаче и всем следующим повторениям.'
              : 'Повторы прекратятся: следующие невыполненные повторения будут удалены.'}
          </p>
        )}

        <FormError>{save.error?.message}</FormError>

        <div className={styles.actions}>
          {task && (
            <Button variant="danger" icon="trash" disabled={isBusy} onClick={() => setConfirmingDelete(true)}>
              Удалить
            </Button>
          )}
          <div className={styles.actionsEnd}>
            <Button variant="outline" onClick={onClose}>
              Отмена
            </Button>
            <Button type="submit" disabled={isBusy}>
              Сохранить
            </Button>
          </div>
        </div>
      </form>
      {task && confirmingDelete && (
        <DeleteTaskDialog task={task} onCancel={() => setConfirmingDelete(false)} onDeleted={onClose} />
      )}
    </Modal>
  )
}
