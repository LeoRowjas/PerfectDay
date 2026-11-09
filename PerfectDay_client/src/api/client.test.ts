import { describe, expect, it } from 'vitest'
import { problemMessage } from './client'

describe('problemMessage', () => {
  it('заменяет английский заголовок фреймворка на русский текст', () => {
    expect(problemMessage(404, { title: 'Not Found' })).toContain('Обновите страницу')
    expect(problemMessage(401, { title: 'Unauthorized' })).toBe('Сессия истекла. Войдите снова.')
  })

  it('показывает русский текст сервера как есть', () => {
    expect(problemMessage(404, { title: 'Эта задача уже удалена. Список обновлён.' })).toBe(
      'Эта задача уже удалена. Список обновлён.',
    )
  })

  it('берёт русскую ошибку поля и пропускает английскую', () => {
    const errors = { request: ['The request field is required.'], start: ['Укажите новое время.'] }
    expect(problemMessage(400, { title: 'One or more validation errors occurred.', errors })).toBe('Укажите новое время.')
  })

  it('не показывает внутренние ошибки сервера и пустые ответы', () => {
    expect(problemMessage(500, { title: 'Ошибка внутри сервера' })).toContain('Не удалось выполнить запрос')
    expect(problemMessage(502, null)).toContain('Не удалось выполнить запрос')
  })
})
