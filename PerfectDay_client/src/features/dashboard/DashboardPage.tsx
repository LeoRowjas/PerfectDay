import { lazy, Suspense, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useChart, useDashboard, useForecast, useProfile, useSetLightened } from '../../api/queries'
import type { BiorhythmCycle, ChartScale, CriticalDate, CycleValue, Dashboard, GoodPeriod, Recommendation } from '../../api/types'
import { NotificationBell } from '../../components/NotificationCenter'
import { Badge, Button, Card, EmptyState, EnergyBar, FormError, Icon, Loading, PageHeader, Segmented } from '../../components/ui'
import type { IconName } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { addDays, formatDayMonth, formatWeekdayDate, minutesToTime, timeToMinutes, todayIso } from '../../lib/date'
import { useCurrentMinutes } from '../../lib/useCurrentMinutes'
import { MOBILE_QUERY, useMediaQuery } from '../../lib/useMediaQuery'
import {
  activityLabel,
  chronotypeName,
  cycleColor,
  cycleLabel,
  cyclePeriodLabel,
  cycleShortLabel,
  formatPercent,
  phaseBadge,
  phaseText,
  taskTypeColor,
} from '../../lib/labels'
import { TaskFormModal } from '../tasks/TaskFormModal'
import { energyAt } from './energy'
import { FirstVisit } from './FirstVisit'
import styles from './DashboardPage.module.css'

// Библиотека графиков большая, поэтому загружается отдельно от остального приложения.
const BiorhythmChart = lazy(() => import('./BiorhythmChart').then((module) => ({ default: module.BiorhythmChart })))

const FORECAST_DAYS = 30
const ENERGY_ORDER: BiorhythmCycle[] = ['intellectual', 'emotional', 'physical']
const energyLabel: Record<BiorhythmCycle, string> = {
  intellectual: 'Интеллектуальная',
  emotional: 'Эмоциональная',
  physical: 'Физическая',
}

const scales: { value: ChartScale; label: string }[] = [
  { value: 'day', label: 'День' },
  { value: 'week', label: 'Неделя' },
  { value: 'month', label: 'Месяц' },
]

const isScale = (value: string | null): value is ChartScale => value === 'day' || value === 'week' || value === 'month'

const phaseTone = { rising: 'success', falling: 'neutral', peak: 'success', trough: 'neutral', critical: 'warning' } as const

const goodPeriodText: Record<BiorhythmCycle, string> = {
  intellectual: 'Экзамены, сложные отчёты, изучение нового: интеллектуальный цикл на пике',
  physical: 'Спорт и нагрузки: физический цикл на пике',
  emotional: 'Переговоры, выступления, встречи: эмоциональный цикл на пике',
}

const lightenAdvice: { icon: IconName; title: string; text: string }[] = [
  { icon: 'shield', title: 'Без рискованных решений', text: 'Подписание договоров, крупные покупки и трудные разговоры лучше перенести.' },
  {
    icon: 'battery-low',
    title: 'Меньше сложных задач',
    text: 'Планировщик поставит не больше 2 часов сложной работы и оставит паузы между задачами.',
  },
  { icon: 'coffee', title: 'Больше отдыха', text: 'Запланируйте прогулку и лёгкие дела. Тренировку лучше заменить растяжкой.' },
  { icon: 'calendar', title: 'Смотрите прогноз', text: 'Важные дела лучше планировать по вкладке «Месяц»: там отмечены лучшие дни.' },
]

function formatRange(from: string, to: string): string {
  if (from === to) return formatDayMonth(from)
  const [fromDay, fromMonth] = formatDayMonth(from).split(' ')
  const [toDay, toMonth] = formatDayMonth(to).split(' ')
  return fromMonth === toMonth ? `${fromDay}–${toDay} ${toMonth}` : `${fromDay} ${fromMonth} – ${toDay} ${toMonth}`
}

function CycleCard({ cycle, isEmpty }: { cycle: CycleValue; isEmpty: boolean }) {
  return (
    <Card className={styles.cycle}>
      <div className={styles.cycleHeader}>
        <span className={styles.cycleDot} style={{ background: cycleColor[cycle.cycle] }} />
        <h3 className={styles.cycleName}>
          <span className={styles.fullName}>{cycleLabel[cycle.cycle]}</span>
          <span className={styles.shortName}>{cycleShortLabel[cycle.cycle]}</span>
        </h3>
        {!isEmpty && <Badge tone={phaseTone[cycle.phase]}>{phaseBadge[cycle.phase]}</Badge>}
      </div>
      <p className={styles.cycleMeta}>
        {cyclePeriodLabel(cycle.periodDays)}
        {!isEmpty && ` · день ${cycle.dayOfCycle}`}
      </p>
      <p className={styles.cycleValueRow}>
        <span className={`${styles.cycleValue} ${isEmpty ? styles.cycleValueEmpty : ''}`}>
          {isEmpty ? '—%' : formatPercent(cycle.percent)}
        </span>
        {!isEmpty && <span className={styles.cycleTrend}>{phaseText[cycle.phase]}</span>}
      </p>
    </Card>
  )
}

function CriticalBanner({ dashboard }: { dashboard: Dashboard }) {
  const warning = dashboard.warnings.find((item) => item.kind === 'criticalDay')
  const lighten = useSetLightened()
  const navigate = useNavigate()
  if (!warning) return null

  return (
    <section className={styles.banner} role="alert">
      <Icon name="alert-triangle" size={22} className={styles.bannerIcon} />
      <div className={styles.bannerText}>
        <h2 className={styles.bannerTitle}>{warning.title}</h2>
        <p>{warning.message}</p>
        <FormError>{lighten.error?.message}</FormError>
      </div>
      <Button
        variant="outline"
        disabled={lighten.isPending}
        onClick={() => lighten.mutate({ date: dashboard.date, isLightened: true }, { onSuccess: () => navigate('/schedule') })}
      >
        Облегчить расписание
      </Button>
    </section>
  )
}

/** Подсказка «что делать прямо сейчас» по окнам рекомендаций. */
function nowTip(recommendations: Recommendation[], now: number): string {
  const windows = recommendations
    .filter((item) => item.activity !== 'routine' && !item.isDeclining)
    .map((item) => ({ ...item, from: timeToMinutes(item.start), to: timeToMinutes(item.end) }))
    .sort((a, b) => a.from - b.from)

  const current = windows.find((item) => item.from <= now && now < item.to)
  const next = windows.find((item) => item.from > now)
  const nextText = next ? ` Дальше лучшее время для дела «${activityLabel[next.activity]}» — с ${next.start}.` : ''

  if (current) return `${current.title}: окно продлится до ${current.end}.${nextText}`
  if (next) return `Сейчас пиков нет: подойдут лёгкие дела.${nextText}`
  return 'Лучшие окна на сегодня прошли. Прогноз на завтра — на вкладке «Неделя».'
}

function EnergyNowPanel({ date, now, recommendations }: { date: string; now: number; recommendations: Recommendation[] }) {
  const chart = useChart(date, 'day', true)
  const energy = chart.data?.scale === 'day' ? energyAt(chart.data.points, now) : undefined
  const windows = [...recommendations].sort((a, b) => timeToMinutes(a.start) - timeToMinutes(b.start))

  return (
    <Card title="Энергия сейчас" aside={<span className={uiStyles.muted}>{minutesToTime(now)}</span>} className={styles.sideCard}>
      {energy ? (
        <ul className={styles.energyList}>
          {ENERGY_ORDER.map((cycle) => (
            <li key={cycle} className={styles.energyRow}>
              <span className={styles.energyLabel}>
                <span className={styles.cycleDot} style={{ background: cycleColor[cycle] }} />
                {energyLabel[cycle]}
                <strong>{formatPercent(energy[cycle])}</strong>
              </span>
              <EnergyBar percent={energy[cycle]} color={cycleColor[cycle]} />
            </li>
          ))}
        </ul>
      ) : (
        <Loading />
      )}

      <h3 className={styles.sideSubtitle}>Лучшие часы сегодня</h3>
      <ul className={styles.hours}>
        {windows.map((item) => (
          <li key={item.activity}>
            <span className={styles.hoursTime}>
              {item.start}–{item.end}
            </span>
            <span className={styles.cycleDot} style={{ background: taskTypeColor[item.activity] }} />
            <span className={styles.hoursActivity}>{activityLabel[item.activity]}</span>
            <strong className={styles.hoursEnergy}>
              {item.activity === 'routine' ? 'спад' : item.isDeclining ? `до ${item.energyTo}%` : `${item.energyFrom}–${item.energyTo}%`}
            </strong>
          </li>
        ))}
      </ul>

      <div className={styles.tip}>
        <span className={styles.tipTitle}>
          <Icon name="sparkles" size={14} />
          Сейчас лучше всего
        </span>
        <p>{nowTip(recommendations, now)}</p>
      </div>
      <Link to="/schedule" className={`${uiStyles.button} ${uiStyles.outline}`}>
        <Icon name="clock" size={16} />
        Открыть расписание
      </Link>
    </Card>
  )
}

function RecommendationsPanel({ recommendations }: { recommendations: Recommendation[] }) {
  return (
    <Card title="Рекомендации на сегодня" className={styles.sideCard}>
      <p className={uiStyles.muted}>Лучшие окна для разных видов деятельности</p>
      {recommendations.length === 0 ? (
        <EmptyState title="Пока пусто" text="Лучшие окна для работы, спорта и общения появятся здесь." />
      ) : (
        <ul className={styles.recommendations}>
          {recommendations.map((item) => (
            <li key={item.activity} className={styles.recommendation}>
              <span className={styles.recommendationTime} style={{ color: taskTypeColor[item.activity] }}>
                <span className={styles.cycleDot} style={{ background: taskTypeColor[item.activity] }} />
                {item.isDeclining ? 'Весь день' : `${item.start}–${item.end}`}
              </span>
              <h3 className={styles.recommendationTitle}>{item.title}</h3>
              <p className={uiStyles.muted}>{item.text}</p>
            </li>
          ))}
        </ul>
      )}
      <Link to="/schedule" className={`${uiStyles.button} ${uiStyles.outline} ${styles.sideAction}`}>
        Открыть план на сегодня
      </Link>
    </Card>
  )
}

function LightenPanel({ date }: { date: string }) {
  const lighten = useSetLightened()
  const navigate = useNavigate()

  return (
    <Card title="Как снизить нагрузку" className={styles.sideCard}>
      <p className={uiStyles.muted}>Рекомендации на критический день</p>
      <ul className={styles.advice}>
        {lightenAdvice.map((item) => (
          <li key={item.title}>
            <span className={styles.adviceIcon}>
              <Icon name={item.icon} size={16} />
            </span>
            <div>
              <h3 className={styles.recommendationTitle}>{item.title}</h3>
              <p className={uiStyles.muted}>{item.text}</p>
            </div>
          </li>
        ))}
      </ul>
      <FormError>{lighten.error?.message}</FormError>
      <Button
        className={styles.sideAction}
        disabled={lighten.isPending}
        onClick={() => lighten.mutate({ date, isLightened: true }, { onSuccess: () => navigate('/schedule') })}
      >
        Применить к расписанию
      </Button>
    </Card>
  )
}

function criticalText(item: CriticalDate): string {
  const names = item.cycles.map((cycle) => cycleLabel[cycle].toLowerCase())
  const list = names.length > 1 ? `${names.slice(0, -1).join(', ')} и ${names[names.length - 1]}` : names[0]
  const subject = names.length > 1 ? `${list.charAt(0).toUpperCase()}${list.slice(1)} циклы пересекают` : `${cycleLabel[item.cycles[0]]} цикл пересекает`
  return `${subject} ноль: нагрузку лучше снизить заранее.`
}

function ImportantDatesPanel({ date, onPlan }: { date: string; onPlan: (date: string) => void }) {
  const forecast = useForecast(date, FORECAST_DAYS, true)
  const periods: GoodPeriod[] = forecast.data?.goodPeriods ?? []
  const critical = forecast.data?.criticalDates ?? []

  return (
    <Card title="Важные даты" className={styles.sideCard}>
      <p className={uiStyles.muted}>Когда планировать большие дела</p>
      {forecast.isPending && <Loading />}
      <FormError>{forecast.error?.message}</FormError>
      {forecast.data && (
        <>
          <h3 className={styles.datesGroup}>Лучшие дни</h3>
          <ul className={styles.dates}>
            {periods.map((period) => (
              <li key={`${period.cycle}-${period.from}`} style={{ borderColor: cycleColor[period.cycle] }}>
                <button type="button" className={styles.dateButton} onClick={() => onPlan(period.from)}>
                  <strong>{formatRange(period.from, period.to)}</strong>
                  <span>{goodPeriodText[period.cycle]}</span>
                </button>
              </li>
            ))}
            {periods.length === 0 && <li className={uiStyles.muted}>В ближайший месяц циклы не выходят на пик.</li>}
          </ul>
          <h3 className={styles.datesGroup}>Критические дни</h3>
          <ul className={styles.dates}>
            {critical.map((item) => (
              <li key={item.date} style={{ borderColor: 'var(--color-warning)' }}>
                <strong>{formatDayMonth(item.date)}</strong>
                <span>{criticalText(item)}</span>
              </li>
            ))}
          </ul>
        </>
      )}
      <Button variant="outline" icon="calendar-plus" className={styles.sideAction} onClick={() => onPlan(periods[0]?.from ?? date)}>
        Запланировать задачу на дату
      </Button>
    </Card>
  )
}

const captions: Record<ChartScale, string> = {
  day: 'Энергия часа — значение цикла на сегодня и суточная волна хронотипа. Подсвечены лучшие окна для умственной работы и спорта.',
  week: 'Значения выше нуля — фаза подъёма, ниже — спад. День, когда цикл пересекает ноль, считается критическим.',
  month: 'Подсвечены критические дни. В эти даты не стоит ставить экзамены, переговоры и важные решения.',
}

function chartSubtitle(scale: ChartScale, date: string, dashboard: Dashboard): string {
  const chronotype = dashboard.chronotype ? ` с учётом хронотипа «${chronotypeName[dashboard.chronotype]}»` : ''
  if (scale === 'day') return `Энергия по часам на ${formatDayMonth(date)}${chronotype}`
  if (scale === 'week') return `Прогноз на 7 дней: ${formatRange(date, addDays(date, 6))}`
  return `Прогноз на 30 дней: ${formatRange(date, addDays(date, 29))}`
}

export function DashboardPage() {
  const date = todayIso()
  const now = useCurrentMinutes()
  const isMobile = useMediaQuery(MOBILE_QUERY)
  const [params, setParams] = useSearchParams()
  const scaleParam = params.get('scale')
  const scale: ChartScale = isScale(scaleParam) ? scaleParam : 'day'
  const [planDate, setPlanDate] = useState<string | null>(null)
  const { data: profile } = useProfile()
  const dashboard = useDashboard(date)
  const data = dashboard.data
  const isComplete = data?.isProfileComplete ?? false
  const isCritical = data?.warnings.some((item) => item.kind === 'criticalDay') ?? false

  const setScale = (next: ChartScale) => setParams(next === 'day' ? {} : { scale: next }, { replace: true })

  if (data?.isProfileComplete && data.isFirstVisit) return <FirstVisit dashboard={data} />

  return (
    <>
      <PageHeader
        title={isMobile ? 'Сегодня' : 'Дашборд'}
        subtitle={
          isMobile && data?.chronotype ? `${formatWeekdayDate(date)} · «${chronotypeName[data.chronotype]}»` : undefined
        }
        action={
          <>
            {data?.chronotype && isComplete && (
              <span className={styles.headerMeta}>
                {formatWeekdayDate(date)} · хронотип «{chronotypeName[data.chronotype]}»
              </span>
            )}
            <NotificationBell />
            {data && !isComplete ? (
              <Link to="/profile" className={`${uiStyles.button} ${uiStyles.primary}`}>
                Указать дату рождения
              </Link>
            ) : (
              !isMobile && (
                <Link to="/schedule" className={`${uiStyles.button} ${uiStyles.primary}`}>
                  <Icon name="clock" size={16} />
                  Открыть расписание
                </Link>
              )
            )}
          </>
        }
      />

      {dashboard.isPending && <Loading />}
      <FormError>{dashboard.error?.message}</FormError>

      {data && (
        <>
          <CriticalBanner dashboard={data} />

          <div className={styles.cycles}>
            {data.cycles.map((cycle) => (
              <CycleCard key={cycle.cycle} cycle={cycle} isEmpty={!isComplete} />
            ))}
          </div>

          <div className={styles.grid}>
            <Card className={styles.chartCard}>
              <div className={styles.chartHeader}>
                <h2 className={uiStyles.cardTitle}>{isMobile ? 'Энергия по часам' : 'Графики биоритмов'}</h2>
                <Segmented label="Масштаб графика" options={scales} value={scale} onChange={setScale} />
              </div>
              {isComplete && profile ? (
                <>
                  <p className={uiStyles.muted}>{chartSubtitle(scale, date, data)}</p>
                  <Suspense fallback={<Loading />}>
                    <BiorhythmChart
                      date={date}
                      scale={scale}
                      nowMinutes={now}
                      workdayStart={profile.workdayStart}
                      workdayEnd={profile.workdayEnd}
                      recommendations={data.recommendations}
                      height={isMobile ? 220 : 400}
                      compact={isMobile}
                    />
                  </Suspense>
                  {!isMobile && <p className={uiStyles.muted}>{captions[scale]}</p>}
                </>
              ) : (
                <EmptyState title="Графики появятся после ввода даты рождения" text="По ней рассчитываются три цикла." />
              )}
            </Card>

            <div className={styles.side}>
              {isComplete && scale === 'day' && <EnergyNowPanel date={date} now={now} recommendations={data.recommendations} />}
              {isComplete && scale === 'week' &&
                (isCritical ? <LightenPanel date={date} /> : <RecommendationsPanel recommendations={data.recommendations} />)}
              {isComplete && scale === 'month' && <ImportantDatesPanel date={date} onPlan={setPlanDate} />}
              {!isComplete && <RecommendationsPanel recommendations={[]} />}
            </div>
          </div>
        </>
      )}

      {planDate && <TaskFormModal defaultDate={planDate} onClose={() => setPlanDate(null)} />}
    </>
  )
}
