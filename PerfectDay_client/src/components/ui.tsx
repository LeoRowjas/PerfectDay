import { useEffect, useRef } from 'react'
import type { ButtonHTMLAttributes, ReactNode } from 'react'
import emptyWave from '../assets/empty-wave.svg'
import styles from './ui.module.css'

type ButtonVariant = 'primary' | 'outline' | 'danger'

export function Button({
  variant = 'primary',
  type = 'button',
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant }) {
  return <button type={type} className={`${styles.button} ${styles[variant]} ${className ?? ''}`} {...props} />
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
  return (
    <label className={`${styles.field} ${className ?? ''}`}>
      <span className={styles.fieldLabel}>{label}</span>
      {children}
      {error && <span className={styles.error}>{error}</span>}
    </label>
  )
}

export function FormError({ children }: { children?: string }) {
  if (!children) return null
  return (
    <p role="alert" className={styles.error}>
      {children}
    </p>
  )
}

export function Card({ title, children, className }: { title?: string; children: ReactNode; className?: string }) {
  return (
    <section className={`${styles.card} ${className ?? ''}`}>
      {title && <h2 className={styles.cardTitle}>{title}</h2>}
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

export function PageHeader({ title, action }: { title: string; action?: ReactNode }) {
  return (
    <header className={styles.pageHeader}>
      <h1 className={styles.pageTitle}>{title}</h1>
      {action}
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

export function Loading() {
  return (
    <p role="status" className={styles.muted}>
      Загрузка…
    </p>
  )
}
