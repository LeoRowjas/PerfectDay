import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useMarkNotificationsRead, useNotifications, useProfile, useUpdateReminders } from '../api/queries'
import type { AppNotification, NotificationKind, ReminderSettings } from '../api/types'
import { Button, FormError, Icon, Toggle } from './ui'
import type { IconName } from './ui'
import styles from './NotificationCenter.module.css'

const SNOOZE_MS = 5 * 60_000
const SNOOZE_CHECK_MS = 30_000

const kindIcon: Record<NotificationKind, IconName> = {
  planReady: 'sun',
  taskStart: 'clock',
  breakSoon: 'coffee',
  criticalTomorrow: 'alert-triangle',
}

/** Только эти напоминания всплывают: остальные спокойно ждут в списке под колокольчиком. */
const isPopup = (item: AppNotification) => item.isUpcoming && !item.isRead && (item.kind === 'taskStart' || item.kind === 'breakSoon')

function useIsProfileComplete() {
  const { data: profile } = useProfile()
  return Boolean(profile?.birthDate && profile.chronotype)
}

function NotificationItem({ item, onRead }: { item: AppNotification; onRead: (id: string) => void }) {
  return (
    <li className={`${styles.item} ${item.isRead ? '' : styles.unread}`}>
      <span className={`${styles.itemIcon} ${styles[item.kind]}`}>
        <Icon name={kindIcon[item.kind]} size={16} />
      </span>
      <button type="button" className={styles.itemBody} onClick={() => onRead(item.id)}>
        <span className={styles.itemHeader}>
          <strong>{item.title}</strong>
          <span className={styles.itemTime}>{item.at}</span>
        </span>
        <span className={styles.itemText}>{item.text}</span>
      </button>
      {item.kind === 'criticalTomorrow' && (
        <Link to="/?scale=month" className={styles.itemLink} onClick={() => onRead(item.id)}>
          Посмотреть прогноз
        </Link>
      )}
      {!item.isRead && <span className={styles.dot} aria-label="Не прочитано" />}
    </li>
  )
}

export function ReminderSettingsForm({ title = 'Настройки напоминаний' }: { title?: string }) {
  const { data: profile } = useProfile()
  const update = useUpdateReminders()
  const settings = profile?.reminders
  if (!settings) return null

  const change = async (patch: Partial<ReminderSettings>) => {
    // Разрешение браузера нужно спросить в ответ на действие пользователя, иначе браузер его отклонит.
    if (patch.browser && 'Notification' in window && Notification.permission === 'default') {
      const permission = await Notification.requestPermission()
      if (permission !== 'granted') return
    }
    update.mutate({ ...settings, ...patch })
  }

  const browserBlocked = 'Notification' in window && Notification.permission === 'denied'

  return (
    <div className={styles.settings}>
      {title && <h3 className={styles.settingsTitle}>{title}</h3>}
      <Toggle label="Напоминать о начале задачи за 10 минут" checked={settings.beforeTask} onChange={(beforeTask) => change({ beforeTask })} />
      <Toggle
        label="Рекомендации по отдыху в критические дни и на спадах"
        checked={settings.rest}
        onChange={(rest) => change({ rest })}
      />
      <Toggle
        label="Уведомления браузера, когда вкладка свёрнута"
        checked={settings.browser && !browserBlocked}
        disabled={browserBlocked || !('Notification' in window)}
        onChange={(browser) => change({ browser })}
      />
      {browserBlocked && <p className={styles.hint}>Браузер запретил уведомления для этого сайта. Разрешите их в настройках браузера.</p>}
      <FormError>{update.error?.message}</FormError>
    </div>
  )
}

export function NotificationBell() {
  const enabled = useIsProfileComplete()
  const notifications = useNotifications(enabled)
  const markRead = useMarkNotificationsRead()
  const [isOpen, setIsOpen] = useState(false)
  const root = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!isOpen) return
    const closeOutside = (event: MouseEvent) => {
      if (!root.current?.contains(event.target as Node)) setIsOpen(false)
    }
    const closeOnEscape = (event: KeyboardEvent) => event.key === 'Escape' && setIsOpen(false)
    document.addEventListener('mousedown', closeOutside)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('mousedown', closeOutside)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [isOpen])

  if (!enabled) return null

  const items = notifications.data?.items ?? []
  const unread = notifications.data?.unreadCount ?? 0
  const read = (ids: string[]) => ids.length > 0 && markRead.mutate(ids)

  return (
    <div className={styles.bellRoot} ref={root}>
      <button
        type="button"
        className={styles.bell}
        aria-label={unread > 0 ? `Уведомления: ${unread} новых` : 'Уведомления'}
        aria-expanded={isOpen}
        onClick={() => setIsOpen(!isOpen)}
      >
        <Icon name="bell" size={18} />
        {unread > 0 && <span className={styles.count}>{unread}</span>}
      </button>

      {isOpen && (
        <div className={styles.panel} role="dialog" aria-label="Уведомления">
          <div className={styles.panelHeader}>
            <h2 className={styles.panelTitle}>Уведомления</h2>
            {unread > 0 && (
              <Button
                variant="link"
                onClick={() => read(items.filter((item) => !item.isRead).map((item) => item.id))}
              >
                Прочитать все
              </Button>
            )}
          </div>
          {items.length === 0 ? (
            <p className={styles.empty}>Пока тихо. Напоминания появятся за 10 минут до задач и перерывов.</p>
          ) : (
            <ul className={styles.list}>
              {items.map((item) => (
                <NotificationItem key={item.id} item={item} onRead={(id) => !item.isRead && read([id])} />
              ))}
            </ul>
          )}
          <FormError>{notifications.error?.message}</FormError>
          <ReminderSettingsForm />
        </div>
      )}
    </div>
  )
}

/** Всплывающее напоминание и уведомление браузера, если вкладка свёрнута. */
export function NotificationToast() {
  const enabled = useIsProfileComplete()
  const { data: profile } = useProfile()
  const notifications = useNotifications(enabled)
  const markRead = useMarkNotificationsRead()
  const [snoozed, setSnoozed] = useState<Record<string, number>>({})
  const [now, setNow] = useState(() => Date.now())
  const announced = useRef(new Set<string>())

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), SNOOZE_CHECK_MS)
    return () => window.clearInterval(timer)
  }, [])

  const popups = (notifications.data?.items ?? []).filter(isPopup)
  const current = popups.find((item) => (snoozed[item.id] ?? 0) <= now)
  const browserEnabled = Boolean(profile?.reminders.browser)

  useEffect(() => {
    if (!browserEnabled || !('Notification' in window) || Notification.permission !== 'granted') return
    for (const item of popups) {
      if (announced.current.has(item.id)) continue
      announced.current.add(item.id)
      if (document.hidden) new Notification(item.title, { body: item.text, tag: item.id })
    }
  }, [popups, browserEnabled])

  if (!current) return null

  return (
    <div className={styles.toast} role="status">
      <span className={styles.toastIcon}>
        <Icon name={kindIcon[current.kind]} size={18} />
      </span>
      <div className={styles.toastBody}>
        <strong>{current.title}</strong>
        <span className={styles.toastText}>{current.text}</span>
        <div className={styles.toastActions}>
          <button type="button" className={styles.toastPrimary} onClick={() => markRead.mutate([current.id])}>
            Понятно
          </button>
          <button
            type="button"
            className={styles.toastSecondary}
            onClick={() => setSnoozed({ ...snoozed, [current.id]: Date.now() + SNOOZE_MS })}
          >
            Напомнить через 5 минут
          </button>
        </div>
      </div>
      <button type="button" className={styles.toastClose} aria-label="Закрыть" onClick={() => markRead.mutate([current.id])}>
        <Icon name="x" size={16} />
      </button>
    </div>
  )
}
