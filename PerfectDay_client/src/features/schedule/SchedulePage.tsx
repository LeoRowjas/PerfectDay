import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  useAddBreak,
  useBackgroundCalendarSync,
  useMoveBlock,
  usePlannedSchedule,
  useSetLightened,
  useSetTaskCompleted,
  useTasks,
} from '../../api/queries'
import type { ScheduleBlock, Task } from '../../api/types'
import { NotificationBell } from '../../components/NotificationCenter'
import { Button, Card, FormError, Icon, Loading, PageHeader } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { addDays, formatRelativeDay, formatWeekdayDate, isIsoDate, todayIso } from '../../lib/date'
import { formatDuration } from '../../lib/duration'
import { pluralize, TASK_TYPES, taskTypeColor, taskTypeLabel } from '../../lib/labels'
import { MOBILE_QUERY, useMediaQuery } from '../../lib/useMediaQuery'
import { TaskFormModal } from '../tasks/TaskFormModal'
import { MobileDay } from './MobileDay'
import { taskStats } from './scheduleUtils'
import type { MoveDraft } from './scheduleUtils'
import { BlockDetails, LoadCard, MovePanel, UnplacedCard, WhyEmpty } from './SidePanels'
import { Timeline } from './Timeline'
import styles from './SchedulePage.module.css'

type Editor = { mode: 'closed' } | { mode: 'new' } | { mode: 'edit'; task: Task }

function Legend() {
  return (
    <ul className={styles.legend}>
      {TASK_TYPES.map((type) => (
        <li key={type}>
          <span className={styles.dot} style={{ background: taskTypeColor[type] }} />
          {taskTypeLabel[type]}
        </li>
      ))}
    </ul>
  )
}

export function SchedulePage() {
  const today = todayIso()
  const isMobile = useMediaQuery(MOBILE_QUERY)
  const [params, setParams] = useSearchParams()
  const dateParam = params.get('date')
  const date = isIsoDate(dateParam) ? dateParam : today

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [draft, setDraft] = useState<MoveDraft | null>(null)
  const [editor, setEditor] = useState<Editor>({ mode: 'closed' })
  const { schedule, current, build, isProfileComplete } = usePlannedSchedule(date)
  useBackgroundCalendarSync(isProfileComplete)
  const move = useMoveBlock()
  const addBreak = useAddBreak()
  const lighten = useSetLightened()
  const setCompleted = useSetTaskCompleted()
  const tasks = useTasks()

  const selected = current?.blocks.find((block) => block.id === selectedId)
  const drafted = draft ? current?.blocks.find((block) => block.id === draft.blockId) : undefined
  const hasBlocks = (current?.blocks.length ?? 0) > 0
  const isPast = date < today
  const isEditable = !isPast && isProfileComplete
  const isBusy = move.isPending || build.isPending || addBreak.isPending
  const stats = current ? taskStats(current) : null

  const goTo = (next: string) => {
    setSelectedId(null)
    setDraft(null)
    build.reset()
    move.reset()
    setParams(next === today ? {} : { date: next })
  }

  const toggleDone = (block: ScheduleBlock) =>
    block.taskId && setCompleted.mutate({ id: block.taskId, isCompleted: !block.isCompleted })

  const applyDraft = () => {
    if (!draft) return
    move.mutate({ date, blockId: draft.blockId, start: draft.start }, { onSuccess: () => setDraft(null) })
  }

  const details = (block: ScheduleBlock) => (
    <BlockDetails
      key={`${block.id}-${block.start}`}
      block={block}
      date={date}
      isEditable={isEditable}
      onStartMove={() => {
        move.reset()
        setDraft({ blockId: block.id, start: block.start })
      }}
      onEditTask={
        block.taskId && tasks.data?.find((task) => task.id === block.taskId)
          ? () => setEditor({ mode: 'edit', task: tasks.data!.find((task) => task.id === block.taskId)! })
          : undefined
      }
    />
  )

  const summary = draft
    ? 'Режим ручной корректировки · перетащите блок или задайте время'
    : [
        formatWeekdayDate(date),
        stats && stats.count > 0 ? `${pluralize(stats.count, ['задача', 'задачи', 'задач'])}, ${formatDuration(stats.minutes)}` : null,
        current ? `рабочий день ${current.workdayStart}–${current.workdayEnd}` : null,
        isPast ? 'прошедший день, только просмотр' : null,
      ]
        .filter(Boolean)
        .join(' · ')

  return (
    <>
      <PageHeader
        title="Расписание дня"
        subtitle={isMobile && stats ? `${pluralize(stats.count, ['задача', 'задачи', 'задач'])} · ${formatDuration(stats.minutes)}` : undefined}
        action={
          <>
            <NotificationBell />
            {isEditable && !isMobile && (
              <>
                <Button variant="outline" icon="refresh" disabled={isBusy || !hasBlocks} onClick={() => build.mutate(date)}>
                  Перестроить
                </Button>
                {!draft && (
                  <Button icon="plus" onClick={() => setEditor({ mode: 'new' })}>
                    Новая задача
                  </Button>
                )}
              </>
            )}
          </>
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
            className={`${uiStyles.segment} ${date === today ? uiStyles.segmentActive : ''} ${styles.dateCurrent}`}
            onClick={() => goTo(today)}
          >
            {isMobile ? formatRelativeDay(date, today) : 'Сегодня'}
          </button>
          <button type="button" className={uiStyles.segment} aria-label="Следующий день" onClick={() => goTo(addDays(date, 1))}>
            →
          </button>
        </div>
        {!isMobile && <span className={styles.summary}>{summary}</span>}
      </div>

      {!isProfileComplete && (
        <p className={styles.notice}>
          Чтобы задачи встали в расписание, <Link to="/profile">укажите дату рождения и хронотип</Link>.
        </p>
      )}
      {current?.isLightened && (
        <div className={styles.lightened}>
          <Icon name="battery-low" size={18} />
          <span className={styles.lightenedText}>Облегчённый день: не больше 2 часов сложной работы, между задачами остаются паузы.</span>
          {isEditable && (
            <Button variant="link" disabled={lighten.isPending} onClick={() => lighten.mutate({ date, isLightened: false })}>
              Вернуть обычный режим
            </Button>
          )}
        </div>
      )}
      <FormError>{build.error?.message ?? schedule.error?.message ?? addBreak.error?.message ?? setCompleted.error?.message}</FormError>

      <div className={styles.grid}>
        <Card title="Шкала дня" aside={isMobile ? undefined : <Legend />} className={styles.timelineCard}>
          {!current && schedule.isPending && <Loading />}
          {current && !isMobile && (
            <Timeline
              schedule={current}
              selectedId={selectedId}
              draft={draft}
              isToday={date === today}
              isEditable={isEditable}
              isBusy={isBusy}
              onSelect={(id) => {
                move.reset()
                setSelectedId(id)
                if (draft && draft.blockId !== id) setDraft(null)
              }}
              onDraft={(next) => {
                move.reset()
                setDraft(next)
              }}
              onToggleDone={toggleDone}
              onSuggest={(action) =>
                addBreak.mutate({
                  date,
                  pause: { start: action.start, durationMinutes: action.durationMinutes, kind: action.kind === 'insertWalk' ? 'walk' : 'rest' },
                })
              }
            />
          )}
          {current && isMobile && (
            <MobileDay
              schedule={current}
              selectedId={selectedId}
              isToday={date === today}
              onSelect={(id) => setSelectedId(id || null)}
              onToggleDone={toggleDone}
              renderDetails={(block) => (draft?.blockId === block.id ? null : details(block))}
            />
          )}
          {current && !hasBlocks && current.unplacedTasks.length === 0 && !isPast && (
            <p className={uiStyles.muted}>На этот день задач нет. Добавьте задачу, и она сама встанет на подходящее время.</p>
          )}
        </Card>

        <div className={styles.side}>
          {draft && drafted ? (
            <MovePanel
              key={draft.blockId}
              block={drafted}
              date={date}
              draft={draft}
              isApplying={move.isPending}
              applyError={move.error?.message}
              onChange={(start) => setDraft({ ...draft, start })}
              onApply={applyDraft}
              onCancel={() => {
                move.reset()
                setDraft(null)
              }}
            />
          ) : (
            <>
              {!isMobile && (selected || !current?.load.isOverloaded) && (
                <Card title="Почему здесь?" className={styles.why}>
                  {selected ? details(selected) : <WhyEmpty />}
                </Card>
              )}
              <LoadCard schedule={current} isEditable={isEditable} />
              {current && !isPast && current.unplacedTasks.length > 0 && <UnplacedCard schedule={current} isEditable={isEditable} />}
            </>
          )}
        </div>
      </div>

      {editor.mode === 'new' && <TaskFormModal defaultDate={date} onClose={() => setEditor({ mode: 'closed' })} />}
      {editor.mode === 'edit' && (
        <TaskFormModal key={editor.task.id} task={editor.task} onClose={() => setEditor({ mode: 'closed' })} />
      )}
    </>
  )
}
