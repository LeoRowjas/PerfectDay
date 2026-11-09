const HOURS_AND_MINUTES = /^(?:(\d+(?:[.,]\d+)?)\s*ч[а-я]*)?\s*(?:(\d+)\s*м[а-я]*)?$/i

/** Разбирает ввод вида «90», «90 мин», «1,5 ч», «1 ч 30 мин». Возвращает минуты или null. */
export function parseDuration(input: string): number | null {
  const text = input.trim().toLowerCase()
  if (!text) return null
  if (/^\d+$/.test(text)) return Number(text)

  const match = HOURS_AND_MINUTES.exec(text)
  if (!match || (!match[1] && !match[2])) return null

  const hours = match[1] ? Number(match[1].replace(',', '.')) : 0
  const minutes = match[2] ? Number(match[2]) : 0
  const total = hours * 60 + minutes
  return Number.isInteger(total) ? total : Math.round(total)
}

export function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes} мин`
  if (minutes % 30 === 0) return `${String(minutes / 60).replace('.', ',')} ч`
  return `${Math.floor(minutes / 60)} ч ${minutes % 60} мин`
}
