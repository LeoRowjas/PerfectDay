import { useAddBreak, useDismissWarning, useMovePreview, useRemoveBreak, useSetLightened, useSetTaskCompleted, useSetTaskDay, useUnpinBlock } from '../../api/queries'
import type { LoadAction, LoadWarning, Schedule, ScheduleBlock, TaskType } from '../../api/types'
import { Link } from 'react-router-dom'
import { Badge, Button, Card, EmptyState, EnergyBar, Field, FormError, Icon } from '../../components/ui'
import type { IconName } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { addDays, formatDayMonth, formatRelativeDay, todayIso } from '../../lib/date'
import { formatDuration } from '../../lib/duration'
import { cycleColor, pluralize, priorityLabelLower, taskTypeColor, taskTypeCycle, taskTypeLabel } from '../../lib/labels'
import { blockMinutes, blockTitle } from './scheduleUtils'
import type { MoveDraft } from './scheduleUtils'
import styles from './SchedulePage.module.css'

const worseTimeTitle: Record<TaskType, string> = {
  intellectual: 'Не лучшее время для умственной работы',
  physical: 'Не лучшее время для тренировки',
  creative: 'Не лучшее время для общения и творчества',
  routine: 'Не лучшее время',
}

const warningIcon: Record<LoadWarning['kind'], IconName> = {
  criticalDay: 'alert-triangle',
  longHardSeries: 'layers',
  tooMuchHardWork: 'battery-low',
  taskOnCycleDecline: 'trending-down',
  switchActivity: 'shuffle',
}

const hours = (minutes: number) => `${String(Math.round((minutes / 60) * 10) / 10).replace('.', ',')} ч`

function energyColor(block: ScheduleBlock): string {
  const cycle = block.taskType ? taskTypeCycle[block.taskType] : null
  return cycle ? cycleColor[cycle] : 'var(--color-primary)'
}

export function BlockDetails({
  block,
  date,
  isEditable,
  onStartMove,
  onEditTask,
}: {
  block: ScheduleBlock
  date: string
  isEditable: boolean
  onStartMove: () => void
  onEditTask?: () => void
}) {
  const unpin = useUnpinBlock()
  const removeBreak = useRemoveBreak()
  const setCompleted = useSetTaskCompleted()
  const isTask = block.kind === 'task'
  const isEvent = block.kind === 'event'

  return (
    <div className={styles.details}>
      <h3 className={styles.detailsTitle}>{blockTitle(block)}</h3>
      {isTask && block.taskType && (
        <div className={styles.chips}>
          <span className={styles.chip}>
            <span className={styles.dot} style={{ background: taskTypeColor[block.taskType] }} />
            {taskTypeLabel[block.taskType]}
          </span>
          {block.priority && <span className={styles.chipMuted}>{priorityLabelLower[block.priority]} приоритет</span>}
          {block.isPinned && <span className={styles.chipMuted}>закреплено</span>}
        </div>
      )}
      {isEvent && (
        <div className={styles.chips}>
          <span className={styles.chipMuted}>
            <Icon name="calendar" size={12} /> событие календаря
          </span>
        </div>
      )}
      <p className={uiStyles.muted}>
        {formatRelativeDay(date, todayIso())}, {block.start}–{block.end} · {formatDuration(blockMinutes(block))}
      </p>

      <div className={styles.explanation}>
        <span className={styles.explanationTitle}>
          <Icon name="sparkles" size={14} />
          Объяснение планировщика
        </span>
        <p>{block.explanation}</p>
      </div>

      {block.energyPercent !== null && (
        <div className={styles.energy}>
          <span className={styles.energyLabel}>
            Энергия в это время <strong>{Math.max(0, block.energyPercent)}%</strong>
          </span>
          <EnergyBar percent={block.energyPercent} color={energyColor(block)} />
        </div>
      )}

      <div className={styles.detailsActions}>
        {block.taskId && (
          <Button
            variant="outline"
            icon={block.isCompleted ? 'refresh' : 'check'}
            disabled={setCompleted.isPending}
            onClick={() => setCompleted.mutate({ id: block.taskId!, isCompleted: !block.isCompleted })}
          >
            {block.isCompleted ? 'Вернуть в работу' : 'Выполнено'}
          </Button>
        )}
        {isEditable && !block.isCompleted && !isEvent && (
          <Button variant="ghost" icon="clock" onClick={onStartMove}>
            Перенести
          </Button>
        )}
        {onEditTask && (
          <Button variant="link" onClick={onEditTask}>
            Изменить задачу
          </Button>
        )}
        {isEvent && (
          <Link to="/profile?tab=calendars" className={uiStyles.muted}>
            Время встречи меняется в календаре. Настройки импорта
          </Link>
        )}
        {isEditable && block.kind === 'break' && (
          <Button variant="link" onClick={() => removeBreak.mutate({ date, blockId: block.id })}>
            Убрать перерыв
          </Button>
        )}
      </div>

      {isEditable && block.isPinned && isTask && (
        <Button variant="link" onClick={() => unpin.mutate({ date, blockId: block.id })}>
          Открепить: пусть планировщик снова двигает этот блок
        </Button>
      )}

      <FormError>{unpin.error?.message ?? setCompleted.error?.message ?? removeBreak.error?.message}</FormError>
    </div>
  )
}

export function MovePanel({
  block,
  date,
  draft,
  isApplying,
  applyError,
  onChange,
  onApply,
  onCancel,
}: {
  block: ScheduleBlock
  date: string
  draft: MoveDraft
  isApplying: boolean
  applyError?: string
  onChange: (start: string) => void
  onApply: () => void
  onCancel: () => void
}) {
  const isChanged = draft.start !== block.start
  const preview = useMovePreview(date, block.id, isChanged ? draft.start : null)
  const data = isChanged ? preview.data : undefined

  return (
    <Card title="Перенос задачи" className={styles.movePanel}>
      <p className={uiStyles.muted}>
        Блок можно перетащить по шкале или задать время вручную. Перенесённая задача закрепляется, остальные перестраиваются вокруг неё.
      </p>
      <div className={styles.moveTask}>
        <span className={styles.dot} style={{ background: block.taskType ? taskTypeColor[block.taskType] : 'var(--color-faint)' }} />
        <div>
          <strong>{blockTitle(block)}</strong>
          <p className={uiStyles.muted}>
            {block.taskType ? `${taskTypeLabel[block.taskType]} · ` : ''}
            {formatDuration(blockMinutes(block))}
          </p>
        </div>
      </div>

      <Field label="Новое начало">
        <input type="time" step={900} required value={draft.start} onChange={(event) => event.target.value && onChange(event.target.value)} />
      </Field>

      {isChanged && preview.isError && <FormError>{preview.error.message}</FormError>}
      {data?.warning && (
        <div className={`${styles.warningCard} ${styles.warning}`}>
          <strong>{block.taskType ? worseTimeTitle[block.taskType] : 'Не лучшее время'}</strong>
          <p>{data.warning}</p>
        </div>
      )}
      {data && !data.warning && data.energyPercent !== null && (
        <p className={`${styles.okLine}`}>
          <Icon name="check-circle" size={16} />
          Энергия в новое время — {Math.max(0, data.energyPercent)}%.
        </p>
      )}

      {data && data.changes.length > 1 && (
        <div className={styles.changes}>
          <h3 className={styles.changesTitle}>Остаток дня пересчитается</h3>
          <ul>
            {data.changes.map((change) => (
              <li key={`${change.title}-${change.to}`}>
                {change.from === change.to ? (
                  <>
                    <Icon name="minus" size={14} />
                    <span className={uiStyles.muted}>{change.title} — без изменений</span>
                  </>
                ) : (
                  <>
                    <Icon name="arrow-right" size={14} />
                    <strong>{change.title}</strong>
                    <span className={styles.changeTimes}>
                      <s>{change.from}</s> {change.to}
                    </span>
                  </>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      <FormError>{applyError}</FormError>
      <div className={styles.moveActions}>
        <Button disabled={!isChanged || isApplying || preview.isError} onClick={onApply}>
          Применить
        </Button>
        <Button variant="outline" onClick={onCancel}>
          Отменить
        </Button>
      </div>
    </Card>
  )
}

function actionLabel(action: LoadAction): string {
  switch (action.kind) {
    case 'addBreak':
      return 'Добавить перерыв'
    case 'insertWalk':
      return 'Вставить прогулку'
    case 'moveTask':
      return action.targetDate ? `Перенести на ${formatDayMonth(action.targetDate)}` : 'Перенести'
    case 'lighten':
      return 'Облегчить день'
  }
}

function WarningCard({ warning, date, isEditable }: { warning: LoadWarning; date: string; isEditable: boolean }) {
  const addBreak = useAddBreak()
  const setDay = useSetTaskDay()
  const lighten = useSetLightened()
  const dismiss = useDismissWarning()
  const action = warning.action
  const isBusy = addBreak.isPending || setDay.isPending || lighten.isPending || dismiss.isPending

  const apply = (item: LoadAction) => {
    if ((item.kind === 'addBreak' || item.kind === 'insertWalk') && item.start && item.durationMinutes) {
      addBreak.mutate({ date, pause: { start: item.start, durationMinutes: item.durationMinutes, kind: item.kind === 'insertWalk' ? 'walk' : 'rest' } })
    } else if (item.kind === 'moveTask' && item.taskId && item.targetDate) {
      setDay.mutate({ id: item.taskId, dueDate: item.targetDate })
    } else if (item.kind === 'lighten') {
      lighten.mutate({ date, isLightened: true })
    }
  }

  return (
    <li className={`${styles.warningCard} ${styles[warning.severity]}`}>
      <strong className={styles.warningTitle}>
        <Icon name={warningIcon[warning.kind]} size={16} />
        {warning.title}
      </strong>
      <p>{warning.message}</p>
      {isEditable && (
        <div className={styles.warningActions}>
          {action && (
            <Button variant="outline" size="small" disabled={isBusy} onClick={() => apply(action)}>
              {actionLabel(action)}
            </Button>
          )}
          <Button variant="link" disabled={isBusy} onClick={() => dismiss.mutate({ date, warningId: warning.id })}>
            {warning.kind === 'taskOnCycleDecline' ? 'Оставить' : 'Игнорировать'}
          </Button>
        </div>
      )}
      <FormError>{addBreak.error?.message ?? setDay.error?.message ?? lighten.error?.message ?? dismiss.error?.message}</FormError>
    </li>
  )
}

export function LoadCard({ schedule, isEditable }: { schedule: Schedule | undefined; isEditable: boolean }) {
  const hasBlocks = (schedule?.blocks.length ?? 0) > 0
  const warnings = schedule?.warnings ?? []
  const load = schedule?.load
  const isOverloaded = Boolean(load?.isOverloaded)

  const badge = isOverloaded ? (
    <Badge tone="warning">перегрузка</Badge>
  ) : warnings.length > 0 ? (
    <Badge tone="warning">{pluralize(warnings.length, ['предупреждение', 'предупреждения', 'предупреждений'])}</Badge>
  ) : undefined

  return (
    <Card title={isOverloaded ? 'Анализ нагрузки' : 'Нагрузка'} aside={badge}>
      {!hasBlocks && <p className={uiStyles.muted}>Предупреждения о перегрузке и советы по отдыху появятся, когда в дне будут задачи.</p>}

      {load && isOverloaded && (
        <div className={styles.stats}>
          <div className={styles.statWarning}>
            <strong>
              {load.longestSeriesCount} · {hours(load.longestSeriesMinutes)}
            </strong>
            <span>сложных подряд</span>
          </div>
          <div className={styles.statWarning}>
            <strong>
              {hours(load.hardMinutes).replace(' ч', '')} из {hours(load.workdayMinutes)}
            </strong>
            <span>сложной работы</span>
          </div>
          <div className={styles.stat}>
            <strong>{load.restMinutes} мин</strong>
            <span>отдыха</span>
          </div>
        </div>
      )}

      {schedule && warnings.length > 0 && (
        <ul className={styles.warningList}>
          {warnings.map((warning) => (
            <WarningCard key={warning.id} warning={warning} date={schedule.date} isEditable={isEditable} />
          ))}
        </ul>
      )}

      {load && hasBlocks && !isOverloaded && (
        <p className={styles.okLine}>
          <Icon name="check-circle" size={16} />
          Сложной работы {hours(load.hardMinutes)} из {hours(load.workdayMinutes)}
          {load.restMinutes > 0 ? ', перерывы есть' : ''}. Перегрузки нет.
        </p>
      )}
    </Card>
  )
}

export function UnplacedCard({ schedule, isEditable }: { schedule: Schedule; isEditable: boolean }) {
  const setDay = useSetTaskDay()
  const tomorrow = addDays(schedule.date, 1)

  return (
    <Card title="Не поместились" aside={<span className={uiStyles.muted}>{schedule.isLightened ? 'облегчённый день' : 'нет свободного окна'}</span>}>
      <ul className={styles.unplacedList}>
        {schedule.unplacedTasks.map((task) => (
          <li key={task.id}>
            <span className={styles.dot} style={{ background: taskTypeColor[task.type] }} />
            <div className={styles.unplacedText}>
              <strong>{task.title}</strong>
              <span className={uiStyles.muted}>
                {taskTypeLabel[task.type]} · {formatDuration(task.durationMinutes)}
              </span>
            </div>
            {isEditable && (
              <Button variant="outline" size="small" disabled={setDay.isPending} onClick={() => setDay.mutate({ id: task.id, dueDate: tomorrow })}>
                На завтра
              </Button>
            )}
          </li>
        ))}
      </ul>
      <FormError>{setDay.error?.message}</FormError>
    </Card>
  )
}

export function WhyEmpty() {
  return <EmptyState title="Выберите блок" text="Здесь будет объяснение, почему задача стоит в это время." />
}
