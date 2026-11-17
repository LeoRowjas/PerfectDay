import { useState } from 'react'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Link } from 'react-router-dom'
import { analyticsReportUrl } from '../../api/client'
import { useAnalytics, useApplyRecommendations } from '../../api/queries'
import type { Analytics, AnalyticsPeriod, DayStat, Insight } from '../../api/types'
import { NotificationBell } from '../../components/NotificationCenter'
import { Button, Card, EmptyState, FormError, Icon, Loading, PageHeader, Segmented } from '../../components/ui'
import type { IconName } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { formatDayMonth, todayIso } from '../../lib/date'
import { pluralize, taskTypeColor, taskTypeFilterLabel } from '../../lib/labels'
import { MOBILE_QUERY, useMediaQuery } from '../../lib/useMediaQuery'
import styles from './AnalyticsPage.module.css'

const PERIODS: { value: AnalyticsPeriod; label: string }[] = [
  { value: 'week', label: 'Неделя' },
  { value: 'month', label: 'Месяц' },
]

const insightIcon = (insight: Insight): IconName => {
  if (insight.kind === 'timeOfDay') return 'sun'
  if (insight.kind === 'postponed') return 'shuffle'
  if (insight.kind === 'criticalDays') return 'alert-triangle'
  return insight.severity === 'warning' ? 'trending-down' : 'trending-up'
}

const typeGenitive: Record<string, string> = {
  intellectual: 'интеллектуальные',
  physical: 'физические',
  creative: 'творческие',
  routine: 'рутинные',
}

function rangeLabel(from: string, to: string): string {
  const [fromDay, toDay] = [Number(from.slice(8)), formatDayMonth(to)]
  return from.slice(0, 7) === to.slice(0, 7) ? `${fromDay}–${toDay}` : `${formatDayMonth(from)} — ${toDay}`
}

function Stat({ label, value, text, tone }: { label: string; value: string; text: string; tone?: 'success' | 'warning' }) {
  return (
    <Card className={styles.stat}>
      <span className={styles.statLabel}>{label}</span>
      <strong className={`${styles.statValue} ${tone ? styles[tone] : ''}`}>{value}</strong>
      <span className={uiStyles.muted}>{text}</span>
    </Card>
  )
}

function Stats({ data }: { data: Analytics }) {
  const period = data.period === 'week' ? 'за неделю' : 'за месяц'
  return (
    <div className={styles.stats}>
      <Stat label="Выполнено по плану" value={`${data.percent}%`} text={`${data.done} из ${pluralize(data.planned, ['задачи', 'задач', 'задач'])} ${period}`} />
      <Stat
        label="Сложное — в пиковые часы"
        value={data.peakPercent === null ? '—' : `${data.peakPercent}%`}
        tone={data.peakPercent !== null && data.peakPercent >= 70 ? 'success' : undefined}
        text={data.peakPercent === null ? 'сложных задач ещё не выполнено' : 'выполнено в окна подъёма цикла'}
      />
      <Stat
        label="Перенесено вручную"
        value={String(data.movedManually)}
        text={data.mostMovedType ? `чаще всего ${typeGenitive[data.mostMovedType]} задачи` : 'планировщик угадывал время'}
      />
      <Stat
        label="В критические дни"
        value={data.criticalPercent === null ? '—' : `${data.criticalPercent}%`}
        tone={data.criticalPercent !== null && data.criticalPercent < data.percent ? 'warning' : undefined}
        text={
          data.criticalPercent === null
            ? 'задач в критические дни не было'
            : data.criticalPercent < data.percent
              ? 'выполнено — нагрузка была выше нормы'
              : 'выполнено — нагрузка была по силам'
        }
      />
    </div>
  )
}

interface Row extends DayStat {
  label: string
}

function DaysChart({ data, isMobile }: { data: Analytics; isMobile: boolean }) {
  const isWeek = data.period === 'week'
  const rows: Row[] = data.days.map((day) => ({
    ...day,
    label: isWeek
      ? new Intl.DateTimeFormat('ru-RU', { weekday: 'short' }).format(new Date(`${day.date}T00:00`)).replace(/^./, (c) => c.toUpperCase())
      : String(Number(day.date.slice(8))),
  }))

  return (
    <>
      <div className={styles.chart}>
        <ResponsiveContainer width="100%" height={isMobile ? 200 : 260}>
          <BarChart data={rows} barGap={4} margin={{ top: 8, right: 0, bottom: 0, left: -24 }}>
            <CartesianGrid vertical={false} stroke="var(--color-border)" />
            <XAxis dataKey="label" tickLine={false} axisLine={false} interval={isWeek ? 0 : 'preserveStartEnd'} fontSize={12} />
            <YAxis allowDecimals={false} tickLine={false} axisLine={false} fontSize={12} />
            <Tooltip
              cursor={{ fill: 'var(--color-bg)' }}
              formatter={(value, name) => [value, name === 'planned' ? 'Запланировано' : 'Выполнено']}
              labelFormatter={(_, payload) => {
                const day = payload?.[0]?.payload as Row | undefined
                return day ? `${formatDayMonth(day.date)}${day.isCritical ? ' · критический день' : ''}` : ''
              }}
            />
            <Bar dataKey="planned" fill="var(--color-border-strong)" radius={[4, 4, 0, 0]} maxBarSize={28} />
            <Bar dataKey="done" fill="var(--color-primary)" radius={[4, 4, 0, 0]} maxBarSize={28} />
          </BarChart>
        </ResponsiveContainer>
      </div>
      {isWeek && (
        <ol className={styles.dayTotals} aria-label="Выполнено по дням">
          {rows.map((row) => (
            <li key={row.date} className={row.isCritical ? styles.criticalDay : undefined} title={row.isCritical ? 'Критический день' : undefined}>
              {row.done}/{row.planned}
            </li>
          ))}
        </ol>
      )}
      <div className={styles.legend}>
        <span>
          <i style={{ background: 'var(--color-border-strong)' }} /> Запланировано
        </span>
        <span>
          <i style={{ background: 'var(--color-primary)' }} /> Выполнено
        </span>
      </div>
    </>
  )
}

function TypeBars({ data }: { data: Analytics }) {
  return (
    <div className={styles.types}>
      <h3 className={styles.subTitle}>По типам задач</h3>
      {data.types.map((type) => (
        <div key={type.type} className={styles.typeRow}>
          <span className={styles.typeName}>
            <i style={{ background: taskTypeColor[type.type] }} />
            {taskTypeFilterLabel[type.type]}
          </span>
          <span className={styles.track}>
            <span style={{ width: `${type.percent}%`, background: taskTypeColor[type.type] }} />
          </span>
          <strong className={styles.typePercent}>{type.planned === 0 ? '—' : `${type.percent}%`}</strong>
        </div>
      ))}
    </div>
  )
}

function Insights({ data }: { data: Analytics }) {
  const apply = useApplyRecommendations()
  const pending = data.upcomingCriticalDates.filter((date) => !data.lightenedDates.includes(date))

  return (
    <Card title="Выводы и рекомендации" className={styles.insightsCard}>
      <span className={uiStyles.muted}>{data.period === 'week' ? 'По итогам недели' : 'По итогам месяца'}</span>
      {data.insights.length === 0 ? (
        <p className={uiStyles.muted}>Выводы появятся, когда накопится хотя бы несколько дней с выполненными задачами.</p>
      ) : (
        <ul className={styles.insights}>
          {data.insights.map((insight) => (
            <li key={insight.kind} className={styles.insight}>
              <span
                className={`${styles.insightIcon} ${styles[`insight_${insight.kind}`]} ${insight.severity === 'warning' ? styles.insightWarning : ''}`}
              >
                <Icon name={insightIcon(insight)} size={16} />
              </span>
              <div>
                <strong>{insight.title}</strong>
                <p>{insight.text}</p>
              </div>
            </li>
          ))}
        </ul>
      )}

      <div className={styles.apply}>
        {apply.data && (
          <p role="status" className={styles.applied}>
            <Icon name="check-circle" size={16} />
            {apply.data.lightenedDates.length > 0 ? (
              <span>
                Облегчены: {apply.data.lightenedDates.map(formatDayMonth).join(', ')}.{' '}
                <Link to={`/schedule?date=${apply.data.lightenedDates[0]}`}>Открыть расписание</Link>
              </span>
            ) : (
              'Всё уже применено.'
            )}
          </p>
        )}
        {!apply.data && (
          <p className={uiStyles.muted}>
            {pending.length > 0
              ? `Критические дни на две недели вперёд (${pending.map(formatDayMonth).join(', ')}) станут облегчёнными: меньше сложной работы и обязательные паузы.`
              : 'Ближайшие две недели критических дней без облегчения нет.'}
          </p>
        )}
        <FormError>{apply.error?.message}</FormError>
        <Button variant="outline" disabled={pending.length === 0 || apply.isPending || Boolean(apply.data)} onClick={() => apply.mutate()}>
          Применить рекомендации к расписанию
        </Button>
      </div>
    </Card>
  )
}

export function AnalyticsPage() {
  const [period, setPeriod] = useState<AnalyticsPeriod>('week')
  const isMobile = useMediaQuery(MOBILE_QUERY)
  const analytics = useAnalytics(period)
  const data = analytics.data

  return (
    <>
      <PageHeader
        title={isMobile ? 'Аналитика' : 'Анализ эффективности'}
        action={
          <>
            <NotificationBell />
            <a className={`${uiStyles.button} ${uiStyles.outline}`} href={analyticsReportUrl(todayIso(), period)} download>
              <Icon name="download" size={16} />
              {isMobile ? 'Отчёт' : 'Скачать отчёт'}
            </a>
          </>
        }
      />
      {analytics.isPending && <Loading />}
      <FormError>{analytics.error?.message}</FormError>

      {data && (
        <div className={styles.page}>
          <Stats data={data} />
          <div className={styles.layout}>
            <Card
              title="План и факт по дням"
              aside={<Segmented label="Период" options={PERIODS} value={period} onChange={setPeriod} />}
              className={styles.planCard}
            >
              <span className={uiStyles.muted}>Задач запланировано и выполнено, {rangeLabel(data.from, data.to)}</span>
              {data.planned === 0 ? (
                <EmptyState
                  title="Пока нечего анализировать"
                  text="Здесь появятся прошедшие дни: сколько задач стояло в расписании и сколько вы отметили выполненными."
                  action={
                    <Link to="/schedule" className={`${uiStyles.button} ${uiStyles.outline}`}>
                      Открыть расписание
                    </Link>
                  }
                />
              ) : (
                <>
                  <DaysChart data={data} isMobile={isMobile} />
                  <TypeBars data={data} />
                </>
              )}
            </Card>
            <Insights data={data} />
          </div>
        </div>
      )}
    </>
  )
}
