import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, NavLink, useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { ApiError, externalLoginUrl } from '../../api/client'
import { useExternalProviders, useLogin, useRegister } from '../../api/queries'
import type { ExternalProvider } from '../../api/types'
import wave1 from '../../assets/wave-1.svg'
import wave2 from '../../assets/wave-2.svg'
import wave3 from '../../assets/wave-3.svg'
import { Button, Field, FormError, Icon } from '../../components/ui'
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
    password: z.string().min(8, 'Пароль слишком короткий: нужно не менее 8 символов.').max(128, 'Слишком длинный пароль.'),
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

const PROVIDERS: { value: ExternalProvider; label: string; mark: string }[] = [
  { value: 'yandex', label: 'Яндекс ID', mark: 'Я' },
  { value: 'vk', label: 'VK ID', mark: 'VK' },
  { value: 'google', label: 'Google', mark: 'G' },
]

/** Вход через внешние сервисы — переход на страницу провайдера; ненастроенные на сервере кнопки неактивны. */
function ExternalLogin({ caption }: { caption: string }) {
  const { data: providers } = useExternalProviders()
  const isEnabled = (provider: ExternalProvider) => providers?.some((p) => p.provider === provider && p.isEnabled) ?? false
  const noneEnabled = providers !== undefined && !providers.some((p) => p.isEnabled)

  return (
    <div className={styles.external}>
      <span className={styles.divider}>{caption}</span>
      <div className={styles.providers}>
        {PROVIDERS.map((provider) =>
          isEnabled(provider.value) ? (
            <a key={provider.value} className={styles.provider} href={externalLoginUrl(provider.value)}>
              <span className={`${styles.mark} ${styles[provider.value]}`} aria-hidden="true">
                {provider.mark}
              </span>
              {provider.label}
            </a>
          ) : (
            <button
              key={provider.value}
              type="button"
              className={styles.provider}
              disabled
              title={`Вход через ${provider.label} не настроен на сервере`}
            >
              <span className={`${styles.mark} ${styles[provider.value]}`} aria-hidden="true">
                {provider.mark}
              </span>
              {provider.label}
            </button>
          ),
        )}
      </div>
      {noneEnabled && <p className={styles.externalHint}>Вход через сервисы пока не настроен на сервере.</p>}
    </div>
  )
}

const EMAIL_TAKEN = 'Этот email уже зарегистрирован. Войдите или укажите другой адрес.'

export function LoginPage() {
  const login = useLogin()
  const form = useForm<z.infer<typeof loginSchema>>({ resolver: zodResolver(loginSchema) })
  const { errors } = form.formState
  const isWrongCredentials = login.error instanceof ApiError && login.error.status === 401
  const [searchParams, setSearchParams] = useSearchParams()
  const externalError = searchParams.get('error')

  return (
    <AuthShell
      heading="Планируйте день в ладу со своими ритмами"
      text="Три цикла, хронотип и ваши задачи — в одном расписании."
    >
      <h2 className={styles.title}>Добро пожаловать</h2>
      {externalError && !isWrongCredentials && (
        <div className={styles.alert} role="alert">
          <Icon name="alert-circle" size={18} />
          <div>
            <strong>Не удалось войти через сервис</strong>
            <p>{externalError}</p>
          </div>
        </div>
      )}
      {isWrongCredentials && (
        <div className={styles.alert} role="alert">
          <Icon name="alert-circle" size={18} />
          <div>
            <strong>Неверный email или пароль</strong>
            <p>Проверьте раскладку клавиатуры и Caps Lock. Если аккаунта ещё нет, выберите «Регистрация».</p>
          </div>
        </div>
      )}
      <form
        className={styles.fields}
        noValidate
        onChange={() => {
          if (isWrongCredentials) login.reset()
          if (externalError) setSearchParams({}, { replace: true })
        }}
        onSubmit={form.handleSubmit((values) => login.mutate(values))}
      >
        <Field label="Email" error={errors.email?.message}>
          <input
            type="email"
            autoComplete="email"
            placeholder="name@example.ru"
            aria-invalid={Boolean(errors.email) || isWrongCredentials}
            {...form.register('email')}
          />
        </Field>
        <Field label="Пароль" error={errors.password?.message}>
          <input
            type="password"
            autoComplete="current-password"
            aria-invalid={Boolean(errors.password) || isWrongCredentials}
            {...form.register('password')}
          />
        </Field>
        {!isWrongCredentials && <FormError>{login.error?.message}</FormError>}
        <Button type="submit" disabled={login.isPending}>
          Войти
        </Button>
      </form>
      <ExternalLogin caption="или войдите через" />
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
        onSubmit={form.handleSubmit(({ email, password }) =>
          registerUser.mutate(
            { email, password },
            {
              // Занятый email — ошибка конкретного поля, поэтому она показывается под ним, а не общим текстом.
              onError: (error) => error instanceof ApiError && error.status === 409 && form.setError('email', { message: EMAIL_TAKEN }),
            },
          ),
        )}
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
        {!(registerUser.error instanceof ApiError && registerUser.error.status === 409) && (
          <FormError>{registerUser.error?.message}</FormError>
        )}
        <Button type="submit" disabled={registerUser.isPending}>
          Зарегистрироваться
        </Button>
      </form>
      <ExternalLogin caption="или зарегистрируйтесь через" />
    </AuthShell>
  )
}
