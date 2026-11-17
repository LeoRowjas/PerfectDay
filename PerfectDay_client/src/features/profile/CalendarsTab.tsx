import { useState } from 'react'
import { useCalendars, useConnectCalendar, useDisconnectCalendar, useSyncCalendars, useUpdateCalendar } from '../../api/queries'
import type { CalendarConnection, CalendarProvider, ImportedEvent } from '../../api/types'
import { Badge, Button, Card, Field, FormError, Icon, Loading, Toggle } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { formatWeekdayDate } from '../../lib/date'
import { pluralize } from '../../lib/labels'
import styles from './CalendarsTab.module.css'

const PROVIDERS: { value: CalendarProvider; title: string; subtitle: string; howTo: string; placeholder: string }[] = [
  {
    value: 'google',
    title: 'Google Календарь',
    subtitle: 'По секретному адресу в формате iCal',
    howTo: 'Google Календарь → Настройки → ваш календарь → «Интеграция календаря» → «Закрытый адрес в формате iCal».',
    placeholder: 'https://calendar.google.com/calendar/ical/…/basic.ics',
  },
  {
    value: 'yandex',
    title: 'Яндекс Календарь',
    subtitle: 'По ссылке на экспорт в формате iCal',
    howTo: 'Яндекс Календарь → настройки календаря → «Экспорт» → скопируйте ссылку в формате iCal.',
    placeholder: 'https://calendar.yandex.ru/export/ics.xml?private_token=…',
  },
  {
    value: 'apple',
    title: 'Apple Календарь',
    subtitle: 'По ссылке на календарь в формате iCal',
    howTo: 'iCloud.com → Календарь → «Поделиться» у календаря → «Общедоступный календарь» → скопируйте ссылку webcal://.',
    placeholder: 'webcal://p01-caldav.icloud.com/published/2/…',
  },
]

const providerName: Record<CalendarProvider, string> = { google: 'Google', yandex: 'Яндекс', apple: 'Apple' }

const syncTime = (iso: string | null) =>
  iso ? new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' }).format(new Date(iso)) : null

function ConnectForm({ provider, onClose }: { provider: (typeof PROVIDERS)[number]; onClose: () => void }) {
  const connect = useConnectCalendar()
  const [url, setUrl] = useState('')

  return (
    <form
      className={styles.connectForm}
      onSubmit={(event) => {
        event.preventDefault()
        connect.mutate({ provider: provider.value, url }, { onSuccess: onClose })
      }}
    >
      <Field label="Ссылка на календарь" error={connect.error?.message}>
        <input
          type="url"
          inputMode="url"
          placeholder={provider.placeholder}
          value={url}
          aria-invalid={Boolean(connect.error)}
          onChange={(event) => setUrl(event.target.value)}
          autoFocus
        />
      </Field>
      <p className={`${uiStyles.muted} ${styles.howTo}`}>
        <Icon name="info" size={14} />
        {provider.howTo} Ссылка секретная: мы храним её только для синхронизации и не показываем целиком.
      </p>
      <div className={styles.formActions}>
        <Button variant="outline" size="small" onClick={onClose}>
          Отмена
        </Button>
        <Button type="submit" size="small" disabled={connect.isPending || url.trim().length === 0}>
          {connect.isPending ? 'Загружаем события…' : 'Подключить'}
        </Button>
      </div>
    </form>
  )
}

function ConnectedSettings({ connection }: { connection: CalendarConnection }) {
  const update = useUpdateCalendar()
  const settings = { importAsBusy: connection.importAsBusy, createTasksFromTagged: connection.createTasksFromTagged }
  const change = (patch: Partial<typeof settings>) => update.mutate({ id: connection.id, settings: { ...settings, ...patch } })

  return (
    <div className={styles.settings}>
      <Toggle
        label="Импортировать события как занятое время"
        checked={connection.importAsBusy}
        disabled={update.isPending}
        onChange={(importAsBusy) => change({ importAsBusy })}
      />
      <Toggle
        label="Создавать задачи из событий с пометкой «#задача»"
        checked={connection.createTasksFromTagged}
        disabled={update.isPending}
        onChange={(createTasksFromTagged) => change({ createTasksFromTagged })}
      />
      <FormError>{update.error?.message}</FormError>
    </div>
  )
}

function ProviderRow({ provider, connection }: { provider: (typeof PROVIDERS)[number]; connection?: CalendarConnection }) {
  const [isConnecting, setIsConnecting] = useState(false)
  const disconnect = useDisconnectCalendar()
  const synced = syncTime(connection?.lastSyncedAt ?? null)

  return (
    <li className={`${styles.provider} ${connection ? styles.connected : ''}`}>
      <div className={styles.providerHeader}>
        <span className={`${styles.providerIcon} ${styles[provider.value]}`}>
          <Icon name="calendar" size={18} />
        </span>
        <div className={styles.providerText}>
          <strong>{provider.title}</strong>
          <span className={uiStyles.muted}>
            {connection ? [connection.name ?? connection.urlHint, synced && `синхронизация ${synced}`].filter(Boolean).join(' · ') : provider.subtitle}
          </span>
        </div>
        {connection ? (
          <div className={styles.providerActions}>
            <Badge tone="success">подключён</Badge>
            <Button variant="link" size="small" disabled={disconnect.isPending} onClick={() => disconnect.mutate(connection.id)}>
              Отключить
            </Button>
          </div>
        ) : (
          !isConnecting && (
            <Button variant="outline" size="small" icon="link" onClick={() => setIsConnecting(true)}>
              Подключить
            </Button>
          )
        )}
      </div>
      {connection?.lastError && (
        <p className={`${uiStyles.error} ${styles.syncError}`}>
          <Icon name="alert-circle" size={14} />
          Последняя синхронизация не удалась: {connection.lastError}
        </p>
      )}
      {connection && <ConnectedSettings connection={connection} />}
      {!connection && isConnecting && <ConnectForm provider={provider} onClose={() => setIsConnecting(false)} />}
      <FormError>{disconnect.error?.message}</FormError>
    </li>
  )
}

function EventItem({ event }: { event: ImportedEvent }) {
  return (
    <li className={`${styles.event} ${event.effect === 'busy' ? styles.eventBusy : ''}`}>
      <div className={styles.eventHeader}>
        <strong>
          {event.isAllDay ? 'Весь день' : `${event.start}–${event.end}`}
          <span className={styles.eventTitle}>{event.title}</span>
        </strong>
        <Badge>{providerName[event.provider]}</Badge>
      </div>
      <span className={uiStyles.muted}>{event.note}</span>
    </li>
  )
}

export function CalendarsTab() {
  const calendars = useCalendars()
  const sync = useSyncCalendars()
  const data = calendars.data

  return (
    <div className={styles.layout}>
      <Card
        title="Внешние календари"
        aside={
          data && data.connections.length > 0 ? (
            <Button variant="ghost" size="small" icon="refresh" disabled={sync.isPending} onClick={() => sync.mutate(true)}>
              {sync.isPending ? 'Обновляем…' : 'Обновить'}
            </Button>
          ) : undefined
        }
      >
        <p className={uiStyles.muted}>События из календарей занимают время в расписании: планировщик не ставит задачи поверх встреч.</p>
        {calendars.isPending && <Loading />}
        <FormError>{calendars.error?.message ?? sync.error?.message}</FormError>
        {data && (
          <ul className={styles.providers}>
            {PROVIDERS.map((provider) => (
              <ProviderRow
                key={provider.value}
                provider={provider}
                connection={data.connections.find((connection) => connection.provider === provider.value)}
              />
            ))}
          </ul>
        )}
      </Card>

      {data && (
        <Card title="Импортировано на сегодня">
          <span className={uiStyles.muted}>
            {formatWeekdayDate(data.date)} · {pluralize(data.events.length, ['событие', 'события', 'событий'])}
          </span>
          {data.events.length === 0 ? (
            <p className={uiStyles.muted}>
              {data.connections.length === 0
                ? 'Подключите календарь, и здесь появятся встречи на сегодня.'
                : 'Сегодня в календарях пусто: весь рабочий день свободен для задач.'}
            </p>
          ) : (
            <ul className={styles.events}>
              {data.events.map((event) => (
                <EventItem key={`${event.title}-${event.start}-${event.provider}`} event={event} />
              ))}
            </ul>
          )}
          {data.advice && (
            <div className={styles.advice}>
              <strong>
                <Icon name="sparkles" size={16} />
                {data.advice.title}
              </strong>
              <p>{data.advice.text}</p>
            </div>
          )}
        </Card>
      )}
    </div>
  )
}
