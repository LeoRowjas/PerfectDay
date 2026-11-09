import { lazy, Suspense, useState } from 'react'
import { Link } from 'react-router-dom'
import { useDashboard, usePlannedSchedule } from '../../api/queries'
import type { ChartScale, CycleValue } from '../../api/types'
import { Card, EmptyState, FormError, Loading, PageHeader, Segmented } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { WarningList } from '../../components/WarningList'
import { todayIso } from '../../lib/date'
import { cycleColor, cycleLabel, cyclePeriodLabel } from '../../lib/labels'
import styles from './DashboardPage.module.css'

// Библиотека графиков большая, поэтому загружается отдельно от остального приложения.
const BiorhythmChart = lazy(() => import('./BiorhythmChart').then((module) => ({ default: module.BiorhythmChart })))

const scales: { value: ChartScale; label: string }[] = [
  { value: 'day', label: 'День' },
  { value: 'week', label: 'Неделя' },
  { value: 'month', label: 'Месяц' },
]

function formatPercent(percent: number): string {
  if (percent > 0) return `+${percent}%`
  return percent < 0 ? `−${Math.abs(percent)}%` : '0%'
}

function CycleCard({ cycle, isEmpty }: { cycle: CycleValue; isEmpty: boolean }) {
  return (
    <Card className={styles.cycle}>
      <div className={styles.cycleHeader}>
        <span className={styles.cycleDot} style={{ background: cycleColor[cycle.cycle] }} />
        <h3 className={styles.cycleName}>{cycleLabel[cycle.cycle]}</h3>
        {cycle.isCritical && <span className={styles.critical}>Критический день</span>}
      </div>
      <p className={uiStyles.muted}>{cyclePeriodLabel(cycle.periodDays)}</p>
      <p className={`${styles.cycleValue} ${isEmpty ? styles.cycleValueEmpty : ''}`}>
        {isEmpty ? '—%' : formatPercent(cycle.percent)}
      </p>
    </Card>
  )
}

function TodayPlan({ date }: { date: string }) {
  const { current } = usePlannedSchedule(date)
  const tasks = current?.blocks.filter((block) => block.kind === 'task') ?? []

  return (
    <Card title="План на сегодня">
      {tasks.length === 0 ? (
        <p className={uiStyles.muted}>
          На сегодня задач нет. <Link to="/tasks">Добавьте задачу</Link>, и она сама встанет в расписание.
        </p>
      ) : (
        <>
          <ul className={styles.plan}>
            {tasks.map((block) => (
              <li key={block.id} className={`${styles.planItem} ${block.isCompleted ? styles.planDone : ''}`}>
                <span className={styles.planTime}>
                  {block.start}–{block.end}
                </span>
                <span className={styles.planTitle}>{block.title}</span>
              </li>
            ))}
          </ul>
          <Link to="/schedule" className={styles.planLink}>
            Открыть расписание
          </Link>
        </>
      )}
    </Card>
  )
}

export function DashboardPage() {
  const date = todayIso()
  const [scale, setScale] = useState<ChartScale>('day')
  const dashboard = useDashboard(date)
  const data = dashboard.data
  const isComplete = data?.isProfileComplete ?? false

  return (
    <>
      <PageHeader
        title="Дашборд"
        action={
          data && !isComplete ? (
            <Link to="/profile" className={`${uiStyles.button} ${uiStyles.primary}`}>
              Указать дату рождения
            </Link>
          ) : undefined
        }
      />

      {dashboard.isPending && <Loading />}
      <FormError>{dashboard.error?.message}</FormError>

      {data && (
        <>
          {data.warnings.length > 0 && <WarningList warnings={data.warnings} />}

          <div className={styles.cycles}>
            {data.cycles.map((cycle) => (
              <CycleCard key={cycle.cycle} cycle={cycle} isEmpty={!isComplete} />
            ))}
          </div>

          <div className={styles.grid}>
            <Card>
              <div className={styles.chartHeader}>
                <h2 className={uiStyles.cardTitle}>Графики биоритмов</h2>
                <Segmented label="Масштаб графика" options={scales} value={scale} onChange={setScale} />
              </div>
              <Suspense fallback={<Loading />}>
                <BiorhythmChart date={date} scale={scale} isProfileComplete={isComplete} />
              </Suspense>
            </Card>

            <div className={styles.side}>
              {isComplete && <TodayPlan date={date} />}
              <Card title="Рекомендации на сегодня" className={styles.recommendationsCard}>
              {data.recommendations.length === 0 ? (
                <EmptyState title="Пока пусто" text="Лучшие окна для работы, спорта и общения появятся здесь." />
              ) : (
                <ul className={styles.recommendations}>
                  {data.recommendations.map((item) => (
                    <li key={item.activity} className={styles.recommendation}>
                      <div className={styles.recommendationHeader}>
                        <h3 className={styles.recommendationTitle}>{item.title}</h3>
                        <span className={styles.recommendationTime}>
                          {item.start}–{item.end}
                        </span>
                      </div>
                      <p className={uiStyles.muted}>{item.text}</p>
                    </li>
                  ))}
                </ul>
              )}
              </Card>
            </div>
          </div>
        </>
      )}
    </>
  )
}
