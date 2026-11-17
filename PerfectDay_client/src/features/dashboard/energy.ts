import type { BiorhythmCycle, ChartPoint } from '../../api/types'
import { timeToMinutes } from '../../lib/date'

export type CycleEnergy = Record<BiorhythmCycle, number>

/** Энергия в произвольную минуту: линейно между часовыми точками графика «День». */
export function energyAt(points: ChartPoint[], minutes: number): CycleEnergy | undefined {
  if (points.length === 0) return undefined

  const index = points.findIndex((point) => timeToMinutes(point.at) > minutes)
  if (index <= 0) {
    const edge = points[index === 0 ? 0 : points.length - 1]
    return { physical: edge.physical, emotional: edge.emotional, intellectual: edge.intellectual }
  }

  const before = points[index - 1]
  const after = points[index]
  const share = (minutes - timeToMinutes(before.at)) / (timeToMinutes(after.at) - timeToMinutes(before.at))
  const mix = (cycle: BiorhythmCycle) => Math.round(before[cycle] + (after[cycle] - before[cycle]) * share)
  return { physical: mix('physical'), emotional: mix('emotional'), intellectual: mix('intellectual') }
}
