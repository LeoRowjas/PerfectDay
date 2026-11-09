import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { useProfile, useUpdateProfile } from '../../api/queries'
import { Button, Card, Field, FormError, Loading, PageHeader } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import { todayIso } from '../../lib/date'
import { chronotypeOptions } from '../../lib/labels'
import styles from './ProfilePage.module.css'

const MIN_BIRTH_DATE = '1900-01-01'

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

export function ProfilePage() {
  const { data: profile } = useProfile()
  const update = useUpdateProfile()
  const navigate = useNavigate()
  const isOnboarding = Boolean(profile) && !(profile?.birthDate && profile.chronotype)
  const form = useForm<ProfileForm>({ resolver: zodResolver(schema) })
  const { errors } = form.formState
  const { reset, control } = form

  useEffect(() => {
    if (!profile) return
    reset({
      birthDate: profile.birthDate ?? '',
      chronotype: profile.chronotype ?? undefined,
      workdayStart: profile.workdayStart,
      workdayEnd: profile.workdayEnd,
    })
  }, [profile, reset])

  const selectedChronotype = useWatch({ control, name: 'chronotype' })

  return (
    <>
      <PageHeader title="Профиль и настройки" />

      {!profile && <Loading />}

      <form
        className={styles.form}
        noValidate
        onChange={() => update.isSuccess && update.reset()}
        onSubmit={form.handleSubmit((values) =>
          // После первого заполнения следующий шаг — задачи: первое расписание должно быть за 2 минуты.
          update.mutate(values, { onSuccess: () => isOnboarding && navigate('/tasks') }),
        )}
      >
        <Card title="Шаг 1. Дата рождения" className={styles.step}>
          <Field label="Дата рождения" error={errors.birthDate?.message} className={styles.birthDate}>
            <input type="date" aria-invalid={Boolean(errors.birthDate)} {...form.register('birthDate')} />
          </Field>
          <p className={uiStyles.muted}>По ней рассчитываются три цикла. Значения появятся на дашборде.</p>
        </Card>

        <Card title="Шаг 2. Хронотип" className={styles.step}>
          <div className={styles.chronotypes}>
            {chronotypeOptions.map((option) => (
              <label
                key={option.value}
                className={`${styles.chronotype} ${selectedChronotype === option.value ? styles.chronotypeSelected : ''}`}
              >
                <input type="radio" value={option.value} {...form.register('chronotype')} />
                <span className={styles.chronotypeTitle}>{option.title}</span>
                <span className={uiStyles.muted}>{option.hint}</span>
              </label>
            ))}
          </div>
          {errors.chronotype && <span className={uiStyles.error}>{errors.chronotype.message}</span>}
          <p className={uiStyles.muted}>Не уверены? Выберите «Голубь»: его можно поменять в любой момент.</p>
        </Card>

        <Card title="Шаг 3. Рабочие часы" className={styles.step}>
          <div className={styles.hours}>
            <Field label="Начало дня" error={errors.workdayStart?.message}>
              <input type="time" aria-invalid={Boolean(errors.workdayStart)} {...form.register('workdayStart')} />
            </Field>
            <Field label="Конец дня" error={errors.workdayEnd?.message}>
              <input type="time" aria-invalid={Boolean(errors.workdayEnd)} {...form.register('workdayEnd')} />
            </Field>
          </div>
        </Card>

        <FormError>{update.error?.message}</FormError>
        <div className={styles.actions}>
          <Button type="submit" disabled={update.isPending}>
            {isOnboarding ? 'Сохранить и перейти к задачам' : 'Сохранить'}
          </Button>
          {update.isSuccess && (
            <p role="status" className={styles.saved}>
              Сохранено. Расписания с сегодняшнего дня обновятся с учётом новых данных.
            </p>
          )}
        </div>
      </form>
    </>
  )
}
