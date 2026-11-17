import type { Repeat, Weekday } from '../api/types'

export const WEEKDAYS: { value: Weekday; short: string }[] = [
  { value: 'monday', short: 'Пн' },
  { value: 'tuesday', short: 'Вт' },
  { value: 'wednesday', short: 'Ср' },
  { value: 'thursday', short: 'Чт' },
  { value: 'friday', short: 'Пт' },
  { value: 'saturday', short: 'Сб' },
  { value: 'sunday', short: 'Вс' },
]

const WEEKDAY_BY_INDEX: Weekday[] = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday']
const MAX_SCAN_DAYS = 400

function parse(iso: string): Date {
  const [year, month, day] = iso.split('-').map(Number)
  return new Date(year, month - 1, day)
}

const toIso = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`

export function occursOn(repeat: Repeat, start: string, iso: string): boolean {
  if (iso < start || (repeat.until && iso > repeat.until)) return false
  return repeat.kind === 'daily' || (repeat.days ?? []).includes(WEEKDAY_BY_INDEX[parse(iso).getDay()])
}

/** Даты повторов начиная со start, не больше limit штук и не дальше until (включительно). */
export function occurrences(repeat: Repeat, start: string, limit: number, until?: string): string[] {
  const result: string[] = []
  const date = parse(start)
  for (let i = 0; i < MAX_SCAN_DAYS && result.length < limit; i++, date.setDate(date.getDate() + 1)) {
    const iso = toIso(date)
    if ((until && iso > until) || (repeat.until && iso > repeat.until)) break
    if (occursOn(repeat, start, iso)) result.push(iso)
  }
  return result
}

/** «11, 13, 16 ноября» или «30 ноября, 2 декабря». */
export function formatDates(dates: string[]): string {
  const month = (iso: string) => new Intl.DateTimeFormat('ru-RU', { month: 'long', day: 'numeric' }).format(parse(iso)).split(' ')[1]
  const parts = dates.map((iso, index) => {
    const day = String(parse(iso).getDate())
    const isLastOfMonth = index === dates.length - 1 || dates[index + 1].slice(0, 7) !== iso.slice(0, 7)
    return isLastOfMonth ? `${day} ${month(iso)}` : day
  })
  return parts.join(', ')
}

export function endOfMonth(iso: string): string {
  const date = parse(iso)
  return toIso(new Date(date.getFullYear(), date.getMonth() + 1, 0))
}

/** «Каждый день» или «По пн, ср, пт». */
export function repeatSummary(repeat: Repeat): string {
  if (repeat.kind === 'daily') return 'Каждый день'
  const days = WEEKDAYS.filter((day) => repeat.days?.includes(day.value)).map((day) => day.short.toLowerCase())
  return days.length === 7 ? 'Каждый день' : `По ${days.join(', ')}`
}
