import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { useDeleteTask, useSaveTask } from '../../api/queries'
import type { Task, TaskPriority } from '../../api/types'
import { Button, Field, FormError, Modal } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { todayIso } from '../../lib/date'
import { formatDuration, parseDuration } from '../../lib/duration'
import { priorityLabel, TASK_TYPES, taskTypeHint, taskTypeLabel } from '../../lib/labels'
import styles from './TaskFormModal.module.css'

const MIN_DURATION = 5
const MAX_DURATION = 720
const PRIORITIES: TaskPriority[] = ['low', 'medium', 'high']
const DURATION_PRESETS = [30, 60, 90, 120]

const schema = z.object({
  title: z.string().trim().min(1, 'Укажите название.').max(200, 'Не длиннее 200 символов.'),
  type: z.enum(['intellectual', 'physical', 'creative', 'routine'], { message: 'Выберите тип задачи.' }),
  dueDate: z.string().min(1, 'Укажите день.'),
  duration: z.string().refine((value) => {
    const minutes = parseDuration(value)
    return minutes !== null && minutes >= MIN_DURATION && minutes <= MAX_DURATION
  }, 'От 5 минут до 12 часов, например, 90 мин.'),
  priority: z.enum(['low', 'medium', 'high']),
})

type TaskForm = z.infer<typeof schema>

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
  const remove = useDeleteTask()
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
        }
      : { title: '', dueDate: defaultDate ?? todayIso(), duration: '', priority: 'medium' },
  })
  const { errors } = form.formState
  const selectedType = useWatch({ control: form.control, name: 'type' })

  const onSubmit = ({ title, type, dueDate, duration, priority }: TaskForm) => {
    save.mutate(
      { id: task?.id, task: { title, type, dueDate, durationMinutes: parseDuration(duration)!, priority } },
      {
        onSuccess: (saved) => {
          onSaved?.(saved)
          onClose()
        },
      },
    )
  }

  const isBusy = save.isPending || remove.isPending

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
          <Field label="День" error={errors.dueDate?.message}>
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

        <p className={uiStyles.muted}>В этот день задача сама встанет в расписание. Если её не выполнить, она перейдёт на следующий день.</p>

        <FormError>{save.error?.message ?? remove.error?.message}</FormError>

        <div className={styles.actions}>
          {task && (
            <Button
              variant="danger"
              disabled={isBusy}
              onClick={() => (confirmingDelete ? remove.mutate(task.id, { onSuccess: onClose }) : setConfirmingDelete(true))}
            >
              {confirmingDelete ? 'Точно удалить?' : 'Удалить'}
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
    </Modal>
  )
}
