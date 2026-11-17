import { useDeleteTask } from '../../api/queries'
import type { Task } from '../../api/types'
import { ConfirmDialog } from '../../components/ui'
import { formatDayMonth, todayIso } from '../../lib/date'

/** Подтверждение удаления; у повторяющейся задачи можно удалить и все следующие повторения. */
export function DeleteTaskDialog({ task, onCancel, onDeleted }: { task: Task; onCancel: () => void; onDeleted: () => void }) {
  const remove = useDeleteTask()
  const today = todayIso()
  const day = formatDayMonth(task.dueDate < today ? today : task.dueDate)
  const run = (withFollowing: boolean) => remove.mutate({ id: task.id, withFollowing }, { onSuccess: onDeleted })

  return task.repeat ? (
    <ConfirmDialog
      title="Удалить повторяющуюся задачу?"
      text={`«${task.title}» повторяется. Можно удалить только задачу на ${day} или её вместе со всеми следующими повторениями: тогда серия остановится. Выполненные задачи останутся в истории.`}
      confirmLabel="Только эту"
      isBusy={remove.isPending}
      error={remove.error?.message}
      onCancel={onCancel}
      onConfirm={() => run(false)}
      extraAction={{ label: 'Эту и следующие', onClick: () => run(true) }}
    />
  ) : (
    <ConfirmDialog
      title="Удалить задачу?"
      text={`«${task.title}» исчезнет из списка задач и из расписания на ${day}. Освободившееся время займут другие задачи. Отменить удаление нельзя.`}
      confirmLabel="Удалить"
      isBusy={remove.isPending}
      error={remove.error?.message}
      onCancel={onCancel}
      onConfirm={() => run(false)}
    />
  )
}
