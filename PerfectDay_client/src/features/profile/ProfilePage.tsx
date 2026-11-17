import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { useLogout, useProfile, useUpdateProfile } from '../../api/queries'
import type { ExternalProvider, Profile } from '../../api/types'
import { NotificationBell, ReminderSettingsForm } from '../../components/NotificationCenter'
import { Button, Card, Field, FormError, Icon, Loading, PageHeader } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { daysBetween, isIsoDate, todayIso } from '../../lib/date'
import { chronotypeOptions } from '../../lib/labels'
import { CalendarsTab } from './CalendarsTab'
import styles from './ProfilePage.module.css'

const TABS = [
  { value: 'profile', label: 'Профиль' },
  { value: 'notifications', label: 'Уведомления' },
  { value: 'calendars', label: 'Календари' },
] as const
type Tab = (typeof TABS)[number]['value']

const externalName: Record<ExternalProvider, string> = { yandex: 'Яндекс ID', vk: 'VK ID', google: 'Google' }

const MIN_BIRTH_DATE = '1900-01-01'
const PERIODS = { physical: 23, emotional: 28, intellectual: 33 }

const schema = z
  .object({
    birthDate: z
      .string()
      .min(1, 'Укажите дату рождения.')
      .refine((value) => value >= MIN_BIRTH_DATE && value <= todayIso(), 'Проверьте дату рождения.'),
    chronotype: z.enum(['lark', 'dove', 'owl'], { message: 'Выберите хронотип.' }),
    workdayStart: z.string().min(1, 'Укажите начало дня.'),
    workdayEnd: z.string().min(1, 'Укажите конец дня.'),
  })
  .refine((form) => form.workdayEnd > form.workdayStart, {
    message: 'Конец дня должен быть позже начала.',
    path: ['workdayEnd'],
  })

type ProfileForm = z.infer<typeof schema>

const toForm = (profile: Profile): Partial<ProfileForm> => ({
  birthDate: profile.birthDate ?? '',
  chronotype: profile.chronotype ?? undefined,
  workdayStart: profile.workdayStart,
  workdayEnd: profile.workdayEnd,
})

const chronotypeTitle = (value: string | null | undefined) => chronotypeOptions.find((option) => option.value === value)?.title

function cycleDaysHint(birthDate: string | undefined): string {
  if (!isIsoDate(birthDate ?? null) || birthDate! > todayIso()) return 'По дате рождения рассчитываются три цикла. Значения появятся на дашборде.'
  const days = daysBetween(birthDate!, todayIso())
  const day = (period: number) => (days % period) + 1
  return (
    `Сегодня ${days.toLocaleString('ru-RU')}-й день: физический цикл — день ${day(PERIODS.physical)}, ` +
    `эмоциональный — ${day(PERIODS.emotional)}, интеллектуальный — ${day(PERIODS.intellectual)}.`
  )
}

function changesText(profile: Profile, values: Partial<ProfileForm>): string[] {
  const changes: string[] = []
  if (values.birthDate !== (profile.birthDate ?? '')) changes.push('Дата рождения изменена.')
  if (values.chronotype !== profile.chronotype) {
    changes.push(`Хронотип изменён: «${chronotypeTitle(profile.chronotype) ?? 'не выбран'}» → «${chronotypeTitle(values.chronotype)}».`)
  }
  if (values.workdayStart !== profile.workdayStart || values.workdayEnd !== profile.workdayEnd) {
    changes.push(`Рабочие часы: ${profile.workdayStart}–${profile.workdayEnd} → ${values.workdayStart}–${values.workdayEnd}.`)
  }
  return changes
}

function AccountCard({ profile }: { profile: Profile }) {
  const logout = useLogout()
  const since = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' }).format(new Date(profile.createdAt))

  return (
    <Card title="Аккаунт">
      <div className={styles.account}>
        <span className={styles.avatar}>{profile.email.charAt(0).toUpperCase()}</span>
        <div className={styles.accountText}>
          <strong>{profile.email}</strong>
          <span className={uiStyles.muted}>В «Идеальном Дне» с {since.replace(' г.', '')}</span>
          {profile.externalLogins.length > 0 && (
            <span className={uiStyles.muted}>
              Вход через {profile.externalLogins.map((provider) => externalName[provider]).join(', ')}
              {profile.hasPassword ? ' или по паролю' : ''}
            </span>
          )}
        </div>
      </div>
      <Button variant="outline" icon="log-out" disabled={logout.isPending} onClick={() => logout.mutate()}>
        Выйти из аккаунта
      </Button>
      <FormError>{logout.error?.message}</FormError>
    </Card>
  )
}

function NotificationsTab() {
  return (
    <div className={styles.layout}>
      <Card title="Напоминания">
        <p className={uiStyles.muted}>
          Напоминания приходят внутри приложения: колокольчик в шапке и всплывающее окно. Уведомления браузера покажут их, даже когда
          вкладка свёрнута.
        </p>
        <ReminderSettingsForm title="" />
      </Card>
    </div>
  )
}

export function ProfilePage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const tabParam = searchParams.get('tab')
  const tab: Tab = TABS.some((t) => t.value === tabParam) ? (tabParam as Tab) : 'profile'
  const { data: profile } = useProfile()
  const update = useUpdateProfile()
  const navigate = useNavigate()
  const isOnboarding = Boolean(profile) && !(profile?.birthDate && profile.chronotype)
  const form = useForm<ProfileForm>({ resolver: zodResolver(schema) })
  const { errors } = form.formState
  const { reset, control } = form

  useEffect(() => {
    if (profile) reset(toForm(profile))
  }, [profile, reset])

  const values = useWatch({ control })
  const changes = profile && !isOnboarding ? changesText(profile, values) : []
  const step = (number: number, title: string) => (isOnboarding ? `Шаг ${number}. ${title}` : title)

  return (
    <>
      <PageHeader title="Профиль и настройки" action={<NotificationBell />} />

      {!profile && <Loading />}

      {profile && !isOnboarding && (
        <nav className={styles.tabs} aria-label="Разделы профиля">
          {TABS.map((item) => (
            <button
              key={item.value}
              type="button"
              aria-pressed={tab === item.value}
              className={`${styles.tab} ${tab === item.value ? styles.tabActive : ''}`}
              onClick={() => setSearchParams(item.value === 'profile' ? {} : { tab: item.value }, { replace: true })}
            >
              {item.label}
            </button>
          ))}
        </nav>
      )}

      {profile && !isOnboarding && tab === 'notifications' && <NotificationsTab />}
      {profile && !isOnboarding && tab === 'calendars' && <CalendarsTab />}

      {profile && (isOnboarding || tab === 'profile') && (
        <div className={styles.layout}>
          <form
            className={styles.form}
            noValidate
            onChange={() => update.isSuccess && update.reset()}
            onSubmit={form.handleSubmit((next) =>
              // После первого заполнения дашборд предложит добавить задачи: первое расписание должно быть за 2 минуты.
              update.mutate(next, { onSuccess: () => isOnboarding && navigate('/') }),
            )}
          >
            <Card title={step(1, 'Дата рождения')}>
              <Field label="Дата рождения" error={errors.birthDate?.message} className={styles.birthDate}>
                <input type="date" aria-invalid={Boolean(errors.birthDate)} {...form.register('birthDate')} />
              </Field>
              <p className={`${uiStyles.muted} ${styles.hint}`}>
                <Icon name="info" size={14} />
                {cycleDaysHint(values.birthDate)}
              </p>
            </Card>

            <Card title={step(2, 'Хронотип')}>
              <div className={styles.chronotypes}>
                {chronotypeOptions.map((option) => (
                  <label
                    key={option.value}
                    className={`${styles.chronotype} ${values.chronotype === option.value ? styles.chronotypeSelected : ''}`}
                  >
                    <input type="radio" value={option.value} {...form.register('chronotype')} />
                    <span className={styles.chronotypeHeader}>
                      <span className={styles.chronotypeTitle}>{option.title}</span>
                      <span className={styles.radioMark} aria-hidden="true">
                        {values.chronotype === option.value && <Icon name="check" size={12} />}
                      </span>
                    </span>
                    <span className={uiStyles.muted}>{option.hint}</span>
                  </label>
                ))}
              </div>
              {errors.chronotype && <FormError>{errors.chronotype.message}</FormError>}
              {isOnboarding && <p className={uiStyles.muted}>Не уверены? Выберите «Голубь»: его можно поменять в любой момент.</p>}
            </Card>

            <Card title={step(3, 'Рабочие часы')}>
              <div className={styles.hours}>
                <Field label="Начало дня" error={errors.workdayStart?.message}>
                  <input type="time" aria-invalid={Boolean(errors.workdayStart)} {...form.register('workdayStart')} />
                </Field>
                <Field label="Конец дня" error={errors.workdayEnd?.message}>
                  <input type="time" aria-invalid={Boolean(errors.workdayEnd)} {...form.register('workdayEnd')} />
                </Field>
              </div>
              <p className={uiStyles.muted}>Планировщик ставит задачи только в эти часы. Обед добавляется через 3 часа после начала дня.</p>
            </Card>

            <FormError>{update.error?.message}</FormError>
            <div className={styles.actions}>
              <Button type="submit" disabled={update.isPending || (!isOnboarding && changes.length === 0)}>
                {isOnboarding ? 'Сохранить и продолжить' : 'Сохранить'}
              </Button>
              {changes.length > 0 && (
                <Button variant="link" onClick={() => reset(toForm(profile))}>
                  Отменить изменения
                </Button>
              )}
              {update.isSuccess && (
                <p role="status" className={styles.saved}>
                  <Icon name="check-circle" size={16} />
                  Сохранено. Расписания с сегодняшнего дня обновятся с учётом новых данных.
                </p>
              )}
            </div>
          </form>

          <div className={styles.side}>
            <AccountCard profile={profile} />
            {changes.length > 0 && (
              <section className={styles.changes}>
                <h2 className={styles.changesTitle}>
                  <Icon name="refresh" size={16} />
                  Что изменится после сохранения
                </h2>
                <p>
                  {changes.join(' ')} Расписания начиная с сегодняшнего дня будут построены заново, закреплённые блоки сбросятся. Прошедшие
                  дни не изменятся.
                </p>
              </section>
            )}
          </div>
        </div>
      )}
    </>
  )
}
