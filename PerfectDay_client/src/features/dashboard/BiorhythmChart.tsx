import { useState } from 'react'
import { CartesianGrid, Legend, Line, LineChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useChart } from '../../api/queries'
import type { BiorhythmCycle, ChartPoint, ChartScale } from '../../api/types'
import { FormError } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { formatDate, formatShortDate } from '../../lib/date'
import { cycleColor, cycleLabel } from '../../lib/labels'
import styles from './BiorhythmChart.module.css'

const CHART_HEIGHT = 440
const CYCLES: BiorhythmCycle[] = ['physical', 'emotional', 'intellectual']
// Без числовых данных Recharts не рисует ось Y, а в макете пустой график показывает шкалу.
// Поэтому пустому графику передаются значения-заглушки, а линии остаются прозрачными.
const EMPTY_DATA = [
  { at: 'start', physical: 0, emotional: 0, intellectual: 0 },
  { at: 'end', physical: 0, emotional: 0, intellectual: 0 },
]

const captions: Record<ChartScale, string> = {
  day: 'Энергия по часам: значение цикла на сегодня и суточная кривая вашего хронотипа.',
  week: 'Значения циклов по дням: три дня назад и три дня вперёд. Пунктиром отмечены критические дни.',
  month: 'Значения циклов по дням: неделя назад и три недели вперёд. Пунктиром отмечены критические дни.',
}

const formatAxisTick = (scale: ChartScale) => (value: string) => (scale === 'day' ? value : formatShortDate(value))
const formatPercentTick = (value: number) => (value > 0 ? `+${value}%` : value < 0 ? `−${-value}%` : '0%')

interface TooltipEntry {
  dataKey?: unknown
  value?: unknown
  payload?: ChartPoint
}

function ChartTooltip({ active, payload, scale }: { active?: boolean; payload?: readonly TooltipEntry[]; scale: ChartScale }) {
  const point = payload?.[0]?.payload
  if (!active || !point) return null

  return (
    <div className={styles.tooltip}>
      <strong>{scale === 'day' ? point.at : formatDate(point.at)}</strong>
      {CYCLES.map((cycle) => (
        <span key={cycle} className={styles.tooltipRow}>
          <span className={styles.tooltipDot} style={{ background: cycleColor[cycle] }} />
          {cycleLabel[cycle]}: {formatPercentTick(point[cycle])}
        </span>
      ))}
      {point.isCritical && <span className={styles.tooltipCritical}>Критический день</span>}
    </div>
  )
}

export function BiorhythmChart({
  date,
  scale,
  isProfileComplete,
}: {
  date: string
  scale: ChartScale
  isProfileComplete: boolean
}) {
  const chart = useChart(date, scale, isProfileComplete)
  const [currentHour] = useState(() => `${String(new Date().getHours()).padStart(2, '0')}:00`)
  const points = chart.data?.scale === scale ? chart.data.points : []

  return (
    <>
      <div className={styles.chart}>
        <ResponsiveContainer width="100%" height={CHART_HEIGHT}>
          <LineChart data={points.length > 0 ? points : EMPTY_DATA} margin={{ top: 8, right: 8, bottom: points.length > 0 ? 0 : 12, left: 0 }}>
            <CartesianGrid vertical={false} stroke="var(--color-border)" />
            <XAxis
              dataKey="at"
              hide={points.length === 0}
              tickFormatter={formatAxisTick(scale)}
              interval="preserveStartEnd"
              minTickGap={24}
              tick={{ fontSize: 12, fill: 'var(--color-muted)' }}
              axisLine={false}
              tickLine={false}
            />
            <YAxis
              domain={[-100, 100]}
              ticks={[-100, -50, 0, 50, 100]}
              tickFormatter={formatPercentTick}
              width={52}
              tick={{ fontSize: 12, fill: 'var(--color-muted)' }}
              axisLine={false}
              tickLine={false}
            />
            <ReferenceLine y={0} stroke="var(--color-border-dashed)" strokeDasharray="6 4" />
            {scale !== 'day' && points.length > 0 && (
              <ReferenceLine
                x={date}
                stroke="var(--color-faint)"
                label={{ value: 'Сегодня', fontSize: 12, fill: 'var(--color-muted)', position: 'insideTop' }}
              />
            )}
            {scale !== 'day' &&
              points
                .filter((point) => point.isCritical)
                .map((point) => (
                  <ReferenceLine key={point.at} x={point.at} stroke="var(--color-warning)" strokeDasharray="3 3" strokeOpacity={0.55} />
                ))}
            {scale === 'day' && points.length > 0 && (
              <ReferenceLine
                x={currentHour}
                stroke="var(--color-faint)"
                label={{ value: 'Сейчас', fontSize: 12, fill: 'var(--color-muted)', position: 'insideTop' }}
              />
            )}
            {points.length > 0 && (
              <Tooltip
                content={(props) => (
                  <ChartTooltip active={props.active} payload={props.payload as readonly TooltipEntry[]} scale={scale} />
                )}
              />
            )}
            {points.length > 0 && (
              <Legend
                verticalAlign="top"
                height={32}
                iconType="plainline"
                formatter={(value: string) => <span className={styles.legend}>{value}</span>}
              />
            )}
            {CYCLES.map((cycle) => (
              <Line
                key={cycle}
                dataKey={cycle}
                name={cycleLabel[cycle]}
                type="monotone"
                stroke={points.length > 0 ? cycleColor[cycle] : 'transparent'}
                strokeWidth={2}
                dot={false}
                isAnimationActive={false}
              />
            ))}
          </LineChart>
        </ResponsiveContainer>
      </div>
      <FormError>{chart.error?.message}</FormError>
      <p className={uiStyles.muted}>
        {isProfileComplete ? captions[scale] : 'Графики появятся после ввода даты рождения.'}
      </p>
    </>
  )
}
