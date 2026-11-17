import { describe, expect, it } from 'vitest'
import { endOfMonth, formatDates, occurrences, repeatSummary } from './recurrence'

describe('recurrence', () => {
  // 9 ноября 2026 — понедельник.
  const weekly = { kind: 'weekly' as const, days: ['monday' as const, 'wednesday' as const, 'friday' as const], until: null }

  it('lists weekly occurrences from the start date', () => {
    expect(occurrences(weekly, '2026-11-11', 3)).toEqual(['2026-11-11', '2026-11-13', '2026-11-16'])
  })

  it('stops at the end date', () => {
    expect(occurrences({ ...weekly, until: '2026-11-13' }, '2026-11-09', 10)).toEqual(['2026-11-09', '2026-11-11', '2026-11-13'])
  })

  it('counts occurrences until the end of month', () => {
    expect(occurrences(weekly, '2026-11-11', 100, endOfMonth('2026-11-11'))).toHaveLength(9)
  })

  it('formats dates of one month compactly', () => {
    expect(formatDates(['2026-11-11', '2026-11-13', '2026-11-16'])).toBe('11, 13, 16 ноября')
    expect(formatDates(['2026-11-30', '2026-12-02'])).toBe('30 ноября, 2 декабря')
  })

  it('summarizes the rule', () => {
    expect(repeatSummary(weekly)).toBe('По пн, ср, пт')
    expect(repeatSummary({ kind: 'daily', days: null, until: null })).toBe('Каждый день')
  })
})
