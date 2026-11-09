import { describe, expect, it } from 'vitest'
import { formatDuration, parseDuration } from './duration'

describe('parseDuration', () => {
  it.each([
    ['90', 90],
    ['90 мин', 90],
    ['40м', 40],
    ['1 ч', 60],
    ['1,5 ч', 90],
    ['1.5ч', 90],
    ['2 часа', 120],
    ['1 ч 30 мин', 90],
  ])('%s → %i', (input, expected) => {
    expect(parseDuration(input)).toBe(expected)
  })

  it.each(['', 'долго', '1 ч ч', '-5'])('отклоняет «%s»', (input) => {
    expect(parseDuration(input)).toBeNull()
  })
})

describe('formatDuration', () => {
  it.each([
    [40, '40 мин'],
    [60, '1 ч'],
    [90, '1,5 ч'],
    [120, '2 ч'],
    [75, '1 ч 15 мин'],
  ])('%i → %s', (minutes, expected) => {
    expect(formatDuration(minutes)).toBe(expected)
  })
})
