import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceArea,
  ReferenceDot,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { useChart } from '../../api/queries'
import type { BiorhythmCycle, ChartScale, Recommendation } from '../../api/types'
import { FormError } from '../../components/ui'
import { formatDate, formatWeekdayShort, minutesToTime, timeToMinutes } from '../../lib/date'
import { cycleColor, cycleLabel, formatPercent } from '../../lib/labels'
import { energyAt } from './energy'
import styles from './BiorhythmChart.module.css'

const CYCLES: BiorhythmCycle[] = ['physical', 'emotional', 'intellectual']
const FIRST_HOUR = 6
const LAST_HOUR = 22
const HOUR_TICKS = [6, 8, 10, 12, 14, 16, 18, 20, 22]
const HIGHLIGHTED: Recommendation['activity'][] = ['intellectual', 'physical']
const highlightFill: Partial<Record<Recommendation['activity'], string>> = {
  intellectual: 'var(--color-primary-soft)',
  physical: 'var(--color-warning-soft)',
}

interface Row {
  x: number
  label: string
  physical: number
  emotional: number
  intellectual: number
  isCritical: boolean
}

interface TooltipEntry {
  payload?: Row
}

function ChartTooltip({ active, payload }: { active?: boolean; payload?: readonly TooltipEntry[] }) {
  const row = payload?.[0]?.payload
  if (!active || !row) return null

  return (
    <div className={styles.tooltip}>
      <strong>{row.label}</strong>
      {CYCLES.map((cycle) => (
        <span key={cycle} className={styles.tooltipRow}>
          <span className={styles.tooltipDot} style={{ background: cycleColor[cycle] }} />
          {cycleLabel[cycle]}: {formatPercent(row[cycle])}
        </span>
      ))}
      {row.isCritical && <span className={styles.tooltipCritical}>Критический день</span>}
    </div>
  )
}

const hours = (time: string) => timeToMinutes(time) / 60

export function BiorhythmChart({
  date,
  scale,
  nowMinutes,
  workdayStart,
  workdayEnd,
  recommendations,
  height = 400,
  compact = false,
}: {
  date: string
  scale: ChartScale
  nowMinutes: number
  workdayStart: string
  workdayEnd: string
  recommendations: Recommendation[]
  height?: number
  compact?: boolean
}) {
  const chart = useChart(date, scale, true)
  const points = chart.data?.scale === scale ? chart.data.points : []
  const isDay = scale === 'day'

  // По дням ось числовая: так подсветка окон и линия «сейчас» встают точно, а не по ближайшей подписи.
  const rows: Row[] = points
    .map((point, index) => ({
      x: isDay ? hours(point.at) : index,
      label: isDay ? point.at : formatDate(point.at),
      physical: point.physical,
      emotional: point.emotional,
      intellectual: point.intellectual,
      isCritical: point.isCritical,
    }))
    .filter((row) => !isDay || (row.x >= FIRST_HOUR && row.x <= LAST_HOUR))

  const nowX = isDay ? nowMinutes / 60 : 0
  const nowValues = isDay ? energyAt(points, nowMinutes) : rows[0]
  const showNow = rows.length > 0 && (!isDay || (nowX >= FIRST_HOUR && nowX <= LAST_HOUR))
  const dayTicks = isDay ? HOUR_TICKS : rows.filter((_, index) => index % (scale === 'week' ? 1 : 2) === 0).map((row) => row.x)

  const xFormatter = (value: number) => {
    if (isDay) return minutesToTime(Math.round(value * 60))
    const point = points[value]
    if (!point) return ''
    return scale === 'week' ? formatWeekdayShort(point.at) : `${Number(point.at.slice(8))}.${Number(point.at.slice(5, 7))}`
  }

  return (
    <>
      <div className={styles.chart} style={{ height }}>
        {rows.length > 0 && (
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={rows} margin={{ top: 8, right: 16, bottom: 0, left: compact ? -12 : 0 }}>
              {isDay && (
                <>
                  <ReferenceArea x1={FIRST_HOUR} x2={hours(workdayStart)} fill="var(--color-bg)" fillOpacity={1} />
                  <ReferenceArea x1={hours(workdayEnd)} x2={LAST_HOUR} fill="var(--color-bg)" fillOpacity={1} />
                  {recommendations
                    .filter((item) => HIGHLIGHTED.includes(item.activity) && !item.isDeclining)
                    .map((item) => (
                      <ReferenceArea
                        key={item.activity}
                        x1={hours(item.start)}
                        x2={hours(item.end)}
                        fill={highlightFill[item.activity]}
                        fillOpacity={1}
                      />
                    ))}
                </>
              )}
              {!isDay &&
                rows
                  .filter((row) => row.isCritical)
                  .map((row) => (
                    <ReferenceArea
                      key={row.x}
                      x1={row.x - 0.35}
                      x2={row.x + 0.35}
                      fill="var(--color-warning-soft)"
                      stroke="var(--color-warning-border)"
                      fillOpacity={1}
                    />
                  ))}
              <CartesianGrid vertical={false} stroke="var(--color-border)" />
              <XAxis
                dataKey="x"
                type="number"
                domain={isDay ? [FIRST_HOUR, LAST_HOUR] : [-0.4, rows.length - 0.6]}
                ticks={dayTicks}
                tickFormatter={xFormatter}
                tick={{ fontSize: 11, fill: 'var(--color-muted)' }}
                axisLine={false}
                tickLine={false}
                interval={compact ? 'preserveStartEnd' : 0}
              />
              <YAxis
                domain={[-100, 100]}
                ticks={[-100, -50, 0, 50, 100]}
                tickFormatter={formatPercent}
                width={compact ? 44 : 52}
                tick={{ fontSize: 11, fill: 'var(--color-muted)' }}
                axisLine={false}
                tickLine={false}
              />
              <ReferenceLine y={0} stroke="var(--color-border-dashed)" strokeDasharray="6 4" />
              <Tooltip content={(props) => <ChartTooltip active={props.active} payload={props.payload as readonly TooltipEntry[]} />} />
              {CYCLES.map((cycle) => (
                <Line
                  key={cycle}
                  dataKey={cycle}
                  name={cycleLabel[cycle]}
                  type="monotone"
                  stroke={cycleColor[cycle]}
                  strokeWidth={2}
                  dot={false}
                  isAnimationActive={false}
                />
              ))}
              {showNow && (
                <ReferenceLine x={nowX} stroke={isDay ? 'var(--color-danger)' : 'var(--color-ink)'} strokeWidth={1.5} />
              )}
              {showNow &&
                nowValues &&
                CYCLES.map((cycle) => (
                  <ReferenceDot
                    key={cycle}
                    x={nowX}
                    y={nowValues[cycle]}
                    r={4}
                    fill="var(--color-surface)"
                    stroke={cycleColor[cycle]}
                    strokeWidth={2}
                  />
                ))}
            </LineChart>
          </ResponsiveContainer>
        )}
      </div>
      <FormError>{chart.error?.message}</FormError>
      <ul className={styles.legend}>
        {CYCLES.map((cycle) => (
          <li key={cycle}>
            <span className={styles.legendDot} style={{ background: cycleColor[cycle] }} />
            {cycleLabel[cycle]}
          </li>
        ))}
        <li>
          <span className={`${styles.legendLine} ${isDay ? styles.legendNow : ''}`} />
          {isDay ? `Сейчас, ${minutesToTime(nowMinutes)}` : 'Сегодня'}
        </li>
        {!compact && (
          <li>
            <span className={`${styles.legendSwatch} ${isDay ? '' : styles.legendCritical}`} />
            {isDay ? 'Нерабочие часы' : 'Критический день'}
          </li>
        )}
      </ul>
    </>
  )
}
