const pad = (value: number) => String(value).padStart(2, '0')

function toIso(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

function fromIso(iso: string): Date {
  const [year, month, day] = iso.split('-').map(Number)
  return new Date(year, month - 1, day)
}

/** Сегодняшняя дата в часовом поясе пользователя. */
export function todayIso(): string {
  return toIso(new Date())
}

export function addDays(iso: string, days: number): string {
  const date = fromIso(iso)
  date.setDate(date.getDate() + days)
  return toIso(date)
}

export function formatDate(iso: string): string {
  const [year, month, day] = iso.split('-')
  return `${day}.${month}.${year}`
}

export function formatShortDate(iso: string): string {
  const [, month, day] = iso.split('-')
  return `${day}.${month}`
}

export function formatLongDate(iso: string): string {
  return new Intl.DateTimeFormat('ru-RU', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(fromIso(iso))
}

export function isIsoDate(value: string | null): value is string {
  return value !== null && /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(fromIso(value).getTime())
}

export function timeToMinutes(time: string): number {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}

export function minutesToTime(minutes: number): string {
  return `${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`
}
