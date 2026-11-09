import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, NavLink } from 'react-router-dom'
import { z } from 'zod'
import { useLogin, useRegister } from '../../api/queries'
import wave1 from '../../assets/wave-1.svg'
import wave2 from '../../assets/wave-2.svg'
import wave3 from '../../assets/wave-3.svg'
import { Button, Field, FormError } from '../../components/ui'
import uiStyles from '../../components/ui.module.css'
import styles from './AuthPage.module.css'

const emailSchema = z.string().trim().min(1, 'Укажите email.').email('Некорректный email.')

const loginSchema = z.object({
  email: emailSchema,
  password: z.string().min(1, 'Укажите пароль.'),
})

const registerSchema = z
  .object({
    email: emailSchema,
    password: z.string().min(8, 'Пароль должен быть не короче 8 символов.').max(128, 'Слишком длинный пароль.'),
    confirmPassword: z.string(),
  })
  .refine((form) => form.password === form.confirmPassword, {
    message: 'Пароли не совпадают.',
    path: ['confirmPassword'],
  })

function AuthShell({ heading, text, children }: { heading: string; text: string; children: React.ReactNode }) {
  const tabClass = ({ isActive }: { isActive: boolean }) =>
    `${uiStyles.segment} ${isActive ? uiStyles.segmentActive : ''}`

  return (
    <div className={styles.page}>
      <section className={styles.promo}>
        <span className={styles.logo}>Идеальный День</span>
        <div className={styles.promoText}>
          <h1 className={styles.heading}>{heading}</h1>
          <p className={styles.lead}>{text}</p>
        </div>
        <div className={styles.waves} aria-hidden="true">
          <img src={wave1} alt="" className={styles.wave1} />
          <img src={wave2} alt="" className={styles.wave2} />
          <img src={wave3} alt="" className={styles.wave3} />
        </div>
      </section>
      <section className={styles.panel}>
        <div className={styles.form}>
          <nav className={uiStyles.segmented} aria-label="Вход или регистрация">
            <NavLink to="/login" className={tabClass}>
              Вход
            </NavLink>
            <NavLink to="/register" className={tabClass}>
              Регистрация
            </NavLink>
          </nav>
          {children}
        </div>
      </section>
    </div>
  )
}

export function LoginPage() {
  const login = useLogin()
  const form = useForm<z.infer<typeof loginSchema>>({ resolver: zodResolver(loginSchema) })
  const { errors } = form.formState

  return (
    <AuthShell
      heading="Планируйте день в ладу со своими ритмами"
      text="Три цикла, хронотип и ваши задачи — в одном расписании."
    >
      <h2 className={styles.title}>Добро пожаловать</h2>
      <form className={styles.fields} noValidate onSubmit={form.handleSubmit((values) => login.mutate(values))}>
        <Field label="Email" error={errors.email?.message}>
          <input
            type="email"
            autoComplete="email"
            placeholder="name@example.ru"
            aria-invalid={Boolean(errors.email)}
            {...form.register('email')}
          />
        </Field>
        <Field label="Пароль" error={errors.password?.message}>
          <input
            type="password"
            autoComplete="current-password"
            aria-invalid={Boolean(errors.password)}
            {...form.register('password')}
          />
        </Field>
        <FormError>{login.error?.message}</FormError>
        <Button type="submit" disabled={login.isPending}>
          Войти
        </Button>
      </form>
      <p className={uiStyles.muted}>
        Нет аккаунта? <Link to="/register">Зарегистрируйтесь</Link>
      </p>
    </AuthShell>
  )
}

export function RegisterPage() {
  const registerUser = useRegister()
  const form = useForm<z.infer<typeof registerSchema>>({ resolver: zodResolver(registerSchema) })
  const { errors } = form.formState

  return (
    <AuthShell heading="Первое расписание — за 2 минуты" text="Email, дата рождения, хронотип — и план дня готов.">
      <h2 className={styles.title}>Создать аккаунт</h2>
      <form
        className={styles.fields}
        noValidate
        onSubmit={form.handleSubmit(({ email, password }) => registerUser.mutate({ email, password }))}
      >
        <Field label="Email" error={errors.email?.message}>
          <input
            type="email"
            autoComplete="email"
            placeholder="name@example.com"
            aria-invalid={Boolean(errors.email)}
            {...form.register('email')}
          />
        </Field>
        <Field label="Пароль" error={errors.password?.message}>
          <input
            type="password"
            autoComplete="new-password"
            placeholder="Не менее 8 символов"
            aria-invalid={Boolean(errors.password)}
            {...form.register('password')}
          />
        </Field>
        <Field label="Повторите пароль" error={errors.confirmPassword?.message}>
          <input
            type="password"
            autoComplete="new-password"
            aria-invalid={Boolean(errors.confirmPassword)}
            {...form.register('confirmPassword')}
          />
        </Field>
        <FormError>{registerUser.error?.message}</FormError>
        <Button type="submit" disabled={registerUser.isPending}>
          Зарегистрироваться
        </Button>
      </form>
    </AuthShell>
  )
}
