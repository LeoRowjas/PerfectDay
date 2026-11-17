import { useEffect, useRef } from 'react'
import type { ButtonHTMLAttributes, CSSProperties, ReactNode } from 'react'
import emptyWave from '../assets/empty-wave.svg'
import styles from './ui.module.css'

const iconUrls = import.meta.glob<string>('../assets/icons/*.svg', { eager: true, query: '?url', import: 'default' })

export type IconName =
  | 'alert-circle'
  | 'alert-triangle'
  | 'arrow-right'
  | 'bar-chart'
  | 'battery-low'
  | 'bell'
  | 'calendar'
  | 'calendar-plus'
  | 'check'
  | 'check-circle'
  | 'clock'
  | 'coffee'
  | 'download'
  | 'external-link'
  | 'footprints'
  | 'grid'
  | 'info'
  | 'layers'
  | 'link'
  | 'log-out'
  | 'map-pin-off'
  | 'minus'
  | 'more-vertical'
  | 'pencil'
  | 'pin'
  | 'plus'
  | 'refresh'
  | 'repeat'
  | 'shield'
  | 'shuffle'
  | 'sparkles'
  | 'sun'
  | 'trash'
  | 'trending-down'
  | 'trending-up'
  | 'x'

/** SVG-иконка цвета текста: файл служит маской, поэтому цвет задаётся через color. */
export function Icon({ name, size = 16, className }: { name: IconName; size?: number; className?: string }) {
  const url = `url("${iconUrls[`../assets/icons/${name}.svg`]}")`
  const style: CSSProperties = { width: size, height: size, maskImage: url, WebkitMaskImage: url }
  return <span aria-hidden="true" className={`${styles.icon} ${className ?? ''}`} style={style} />
}

type ButtonVariant = 'primary' | 'outline' | 'danger' | 'dangerSolid' | 'ghost' | 'link'

export function Button({
  variant = 'primary',
  type = 'button',
  icon,
  size,
  className,
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant; icon?: IconName; size?: 'small' }) {
  return (
    <button
      type={type}
      className={`${styles.button} ${styles[variant]} ${size === 'small' ? styles.small : ''} ${className ?? ''}`}
      {...props}
    >
      {icon && <Icon name={icon} size={size === 'small' ? 14 : 16} />}
      {children}
    </button>
  )
}

export function IconButton({
  icon,
  label,
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { icon: IconName; label: string }) {
  return (
    <button type="button" aria-label={label} title={label} className={`${styles.iconButton} ${className ?? ''}`} {...props}>
      <Icon name={icon} size={18} />
    </button>
  )
}

type BadgeTone = 'neutral' | 'primary' | 'success' | 'warning' | 'danger'

export function Badge({ tone = 'neutral', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={`${styles.badge} ${styles[`badge_${tone}`]}`}>{children}</span>
}

export function Toggle({
  checked,
  onChange,
  label,
  disabled,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
  label: string
  disabled?: boolean
}) {
  return (
    <label className={styles.toggle}>
      <span>{label}</span>
      <input
        type="checkbox"
        role="switch"
        checked={checked}
        disabled={disabled}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span className={styles.toggleTrack} aria-hidden="true" />
    </label>
  )
}

/** Энергия в процентах полосой: отрицательные значения показываются пустой полосой. */
export function EnergyBar({ percent, color }: { percent: number; color: string }) {
  return (
    <span className={styles.energyBar}>
      <span style={{ width: `${Math.max(0, Math.min(100, percent))}%`, background: color }} />
    </span>
  )
}

export function Field({
  label,
  error,
  children,
  className,
}: {
  label: string
  error?: string
  children: ReactNode
  className?: string
}) {
  // Ошибка стоит вне <label>, иначе её текст становится частью названия поля для скринридера.
  return (
    <div className={`${styles.field} ${className ?? ''}`}>
      <label className={styles.fieldInner}>
        <span className={styles.fieldLabel}>{label}</span>
        {children}
      </label>
      {error && (
        <span className={`${styles.error} ${styles.withIcon}`}>
          <Icon name="alert-circle" size={14} />
          {error}
        </span>
      )}
    </div>
  )
}

export function FormError({ children }: { children?: string }) {
  if (!children) return null
  return (
    <p role="alert" className={`${styles.error} ${styles.withIcon}`}>
      <Icon name="alert-circle" size={14} />
      {children}
    </p>
  )
}

export function Card({
  title,
  aside,
  children,
  className,
}: {
  title?: string
  aside?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={`${styles.card} ${className ?? ''}`}>
      {(title || aside) && (
        <div className={styles.cardHeader}>
          {title && <h2 className={styles.cardTitle}>{title}</h2>}
          {aside}
        </div>
      )}
      {children}
    </section>
  )
}

export function EmptyState({ title, text, action }: { title: string; text: string; action?: ReactNode }) {
  return (
    <div className={styles.empty}>
      <img src={emptyWave} alt="" width={40} height={40} />
      <h3 className={styles.emptyTitle}>{title}</h3>
      <p className={styles.muted}>{text}</p>
      {action}
    </div>
  )
}

export function PageHeader({ title, subtitle, action }: { title: string; subtitle?: ReactNode; action?: ReactNode }) {
  return (
    <header className={styles.pageHeader}>
      <div className={styles.pageTitleGroup}>
        <h1 className={styles.pageTitle}>{title}</h1>
        {subtitle && <p className={styles.pageSubtitle}>{subtitle}</p>}
      </div>
      {action && <div className={styles.pageActions}>{action}</div>}
    </header>
  )
}

export function Segmented<T extends string>({
  options,
  value,
  onChange,
  label,
}: {
  options: { value: T; label: string }[]
  value: T
  onChange: (value: T) => void
  label: string
}) {
  return (
    <div className={styles.segmented} role="group" aria-label={label}>
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={option.value === value}
          className={`${styles.segment} ${option.value === value ? styles.segmentActive : ''}`}
          onClick={() => onChange(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  )
}

export function Modal({
  title,
  onClose,
  headerAction,
  children,
}: {
  title: string
  onClose: () => void
  headerAction?: ReactNode
  children: ReactNode
}) {
  const dialog = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const element = dialog.current
    element?.showModal()
    return () => element?.close()
  }, [])

  return (
    <dialog ref={dialog} className={styles.modal} aria-label={title} onCancel={onClose}>
      <div className={styles.modalHeader}>
        <h2 className={styles.modalTitle}>{title}</h2>
        {headerAction}
      </div>
      {children}
    </dialog>
  )
}

/** Подтверждение необратимого действия, например удаления. */
export function ConfirmDialog({
  title,
  text,
  confirmLabel,
  isBusy,
  error,
  onConfirm,
  onCancel,
  extraAction,
}: {
  title: string
  text: string
  confirmLabel: string
  isBusy?: boolean
  error?: string
  onConfirm: () => void
  onCancel: () => void
  /** Второй вариант действия, например «Эту и следующие» для повторяющейся задачи. */
  extraAction?: { label: string; onClick: () => void }
}) {
  const dialog = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const element = dialog.current
    element?.showModal()
    return () => element?.close()
  }, [])

  return (
    <dialog ref={dialog} className={`${styles.modal} ${styles.confirm}`} aria-label={title} onCancel={onCancel}>
      <span className={styles.confirmIcon}>
        <Icon name="trash" size={20} />
      </span>
      <h2 className={styles.modalTitle}>{title}</h2>
      <p className={styles.muted}>{text}</p>
      <FormError>{error}</FormError>
      <div className={styles.confirmActions}>
        <Button variant="outline" onClick={onCancel}>
          Отмена
        </Button>
        {extraAction && (
          <Button variant="danger" disabled={isBusy} onClick={onConfirm}>
            {confirmLabel}
          </Button>
        )}
        <Button variant="dangerSolid" disabled={isBusy} onClick={extraAction?.onClick ?? onConfirm}>
          {extraAction?.label ?? confirmLabel}
        </Button>
      </div>
    </dialog>
  )
}

export function Loading() {
  return (
    <p role="status" className={styles.muted}>
      Загрузка…
    </p>
  )
}
