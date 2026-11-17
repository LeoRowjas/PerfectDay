import { useEffect, useState } from 'react'
import { nowMinutes } from './date'

const CLOCK_TICK_MS = 60_000

/** Текущее время в минутах от полуночи; обновляется раз в минуту. */
export function useCurrentMinutes(): number {
  const [minutes, setMinutes] = useState(nowMinutes)

  useEffect(() => {
    const timer = window.setInterval(() => setMinutes(nowMinutes()), CLOCK_TICK_MS)
    return () => window.clearInterval(timer)
  }, [])

  return minutes
}
