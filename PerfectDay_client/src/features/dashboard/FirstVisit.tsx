import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { useBuildSchedule, useDeleteTask, useProfile, useSaveTask, useTasks } from '../../api/queries'
import type { Dashboard, TaskType } from '../../api/types'
import { Button, Card, FormError, Icon, PageHeader } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { formatDate, todayIso } from '../../lib/date'
import { formatDuration } from '../../lib/duration'
import { activityLabel, chronotypeOptions, cycleColor, cycleLabel, formatPercent, TASK_TYPES, taskTypeColor, taskTypeLabel } from '../../lib/labels'
import styles from './FirstVisit.module.css'

const DURATIONS = [30, 60, 90, 120]
const STEPS_TOTAL = 3

const schema = z.object({
  title: z.string().trim().min(1, 'Напишите, что нужно сделать.').max(200, 'Не длиннее 200 символов.'),
  type: z.enum(['intellectual', 'physical', 'creative', 'routine']),
  duration: z.number(),
})

type QuickTask = z.infer<typeof schema>

function Step({ number, title, text, isDone, isCurrent }: { number: number; title: string; text: string; isDone: boolean; isCurrent: boolean }) {
  return (
    <li className={`${styles.step} ${isCurrent ? styles.stepCurrent : ''}`}>
      <span className={`${styles.stepMark} ${isDone ? styles.stepDone : ''}`}>{isDone ? <Icon name="check" size={16} /> : number}</span>
      <span className={styles.stepText}>
        <strong>
          Шаг {number}. {title}
        </strong>
        <span>{text}</span>
      </span>
    </li>
  )
}

/** Первый вход: профиль заполнен, осталось добавить задачи и построить первое расписание. */
export function FirstVisit({ dashboard }: { dashboard: Dashboard }) {
  const today = todayIso()
  const navigate = useNavigate()
  const { data: profile } = useProfile()
  const tasks = useTasks()
  const save = useSaveTask()
  const remove = useDeleteTask()
  const build = useBuildSchedule()
  const form = useForm<QuickTask>({
    resolver: zodResolver(schema),
    defaultValues: { title: '', type: 'intellectual', duration: 60 },
  })

  const added = (tasks.data ?? []).filter((task) => task.dueDate <= today && !task.isCompleted)
  const stepsDone = 2 + (added.length > 0 ? 1 : 0)
  const chronotype = chronotypeOptions.find((option) => option.value === profile?.chronotype)
  const tip = dashboard.recommendations.find((item) => item.activity !== 'routine' && !item.isDeclining)

  const addTask = ({ title, type, duration }: QuickTask) =>
    save.mutate(
      { task: { title, type, dueDate: today, durationMinutes: duration, priority: 'medium' } },
      { onSuccess: () => form.reset({ title: '', type, duration }) },
    )

  return (
    <>
      <PageHeader title="Добро пожаловать!" action={<span className={uiStyles.muted}>{profile?.email}</span>} />

      <Card>
        <div className={styles.progressHeader}>
          <h2 className={uiStyles.cardTitle}>
            {stepsDone < STEPS_TOTAL ? 'До первого расписания остался один шаг' : 'Всё готово: постройте первое расписание'}
          </h2>
          <span className={uiStyles.muted}>
            {stepsDone} из {STEPS_TOTAL} · около минуты
          </span>
        </div>
        <div className={styles.progress} role="progressbar" aria-valuemin={0} aria-valuemax={STEPS_TOTAL} aria-valuenow={stepsDone}>
          <span style={{ width: `${(stepsDone / STEPS_TOTAL) * 100}%` }} />
        </div>
        <ol className={styles.steps}>
          <Step number={1} title="Дата рождения" text={profile?.birthDate ? formatDate(profile.birthDate) : ''} isDone isCurrent={false} />
          <Step number={2} title="Хронотип" text={chronotype?.title ?? ''} isDone isCurrent={false} />
          <Step
            number={3}
            title="Задачи на сегодня"
            text={added.length > 0 ? `Добавлено: ${added.length}` : 'Добавьте хотя бы одну'}
            isDone={added.length > 0}
            isCurrent
          />
        </ol>
      </Card>

      <div className={styles.grid}>
        <Card title="Добавьте задачи на сегодня">
          <p className={uiStyles.muted}>Достаточно названия и типа. День, длительность и приоритет можно уточнить потом.</p>
          <form className={styles.form} noValidate onSubmit={form.handleSubmit(addTask)}>
            <input
              type="text"
              className={styles.titleInput}
              placeholder="Например, подготовить доклад"
              aria-label="Название задачи"
              aria-invalid={Boolean(form.formState.errors.title)}
              {...form.register('title')}
            />
            <FormError>{form.formState.errors.title?.message}</FormError>

            <Controller
              control={form.control}
              name="type"
              render={({ field }) => (
                <div className={styles.chipRow} role="radiogroup" aria-label="Тип">
                  <span className={styles.chipLabel}>Тип</span>
                  {TASK_TYPES.map((type: TaskType) => (
                    <button
                      key={type}
                      type="button"
                      role="radio"
                      aria-checked={field.value === type}
                      className={`${styles.chip} ${field.value === type ? styles.chipActive : ''}`}
                      onClick={() => field.onChange(type)}
                    >
                      <span className={styles.dot} style={{ background: taskTypeColor[type] }} />
                      {taskTypeLabel[type]}
                    </button>
                  ))}
                </div>
              )}
            />

            <div className={styles.durationRow}>
              <Controller
                control={form.control}
                name="duration"
                render={({ field }) => (
                  <div className={styles.chipRow} role="radiogroup" aria-label="Длительность">
                    <span className={styles.chipLabel}>Длительность</span>
                    {DURATIONS.map((minutes) => (
                      <button
                        key={minutes}
                        type="button"
                        role="radio"
                        aria-checked={field.value === minutes}
                        className={`${styles.chip} ${field.value === minutes ? styles.chipActive : ''}`}
                        onClick={() => field.onChange(minutes)}
                      >
                        {formatDuration(minutes)}
                      </button>
                    ))}
                  </div>
                )}
              />
              <Button type="submit" variant="outline" icon="plus" disabled={save.isPending}>
                Добавить
              </Button>
            </div>
          </form>
          <FormError>{save.error?.message ?? remove.error?.message}</FormError>

          <div className={styles.added}>
            <span className={uiStyles.muted}>Добавлено: {added.length}</span>
            <ul className={styles.addedList}>
              {added.map((task) => (
                <li key={task.id}>
                  <span className={styles.dot} style={{ background: taskTypeColor[task.type] }} />
                  <strong>{task.title}</strong>
                  <span className={uiStyles.muted}>
                    {taskTypeLabel[task.type]} · {formatDuration(task.durationMinutes)}
                  </span>
                  <button type="button" className={styles.remove} aria-label={`Убрать «${task.title}»`} onClick={() => remove.mutate({ id: task.id })}>
                    <Icon name="x" size={14} />
                  </button>
                </li>
              ))}
            </ul>
          </div>

          <div className={styles.buildRow}>
            <Button
              icon="sparkles"
              disabled={added.length === 0 || build.isPending}
              onClick={() => build.mutate(today, { onSuccess: () => navigate('/schedule') })}
            >
              Построить первое расписание
            </Button>
            <span className={uiStyles.muted}>Займёт меньше 2 секунд</span>
          </div>
          <FormError>{build.error?.message}</FormError>
        </Card>

        <Card title="Ваши биоритмы уже посчитаны" className={styles.side}>
          <p className={uiStyles.muted}>По дате рождения на сегодня:</p>
          <ul className={styles.cycles}>
            {dashboard.cycles.map((cycle) => (
              <li key={cycle.cycle}>
                <span className={styles.dot} style={{ background: cycleColor[cycle.cycle] }} />
                {cycleLabel[cycle.cycle]}
                <strong>{formatPercent(cycle.percent)}</strong>
              </li>
            ))}
          </ul>
          {tip && (
            <div className={styles.tip}>
              <span className={styles.tipTitle}>
                <Icon name="sparkles" size={14} />
                Совет на сегодня
              </span>
              <p>
                Сегодня хороший день для дела «{activityLabel[tip.activity]}». Добавьте такую задачу — планировщик поставит её на{' '}
                {tip.start}–{tip.end}.
              </p>
            </div>
          )}
          <p className={`${uiStyles.muted} ${styles.footnote}`}>Графики и прогноз на неделю появятся на дашборде после первого расписания.</p>
        </Card>
      </div>
    </>
  )
}
