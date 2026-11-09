import type { LoadWarning } from '../api/types'
import styles from './WarningList.module.css'

export function WarningList({ warnings }: { warnings: LoadWarning[] }) {
  return (
    <ul className={styles.list}>
      {warnings.map((warning) => (
        <li key={warning.message} className={`${styles.item} ${styles[warning.severity]}`}>
          {warning.message}
        </li>
      ))}
    </ul>
  )
}
