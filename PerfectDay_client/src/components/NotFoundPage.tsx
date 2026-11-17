import { Link } from 'react-router-dom'
import { useProfile } from '../api/queries'
import { AppLayout } from './AppLayout'
import { Icon } from './ui'
import styles from './NotFoundPage.module.css'
import uiStyles from './ui.module.css'

function NotFound({ isSignedIn }: { isSignedIn: boolean }) {
  return (
    <div className={styles.page}>
      <span className={styles.icon}>
        <Icon name="map-pin-off" size={28} />
      </span>
      <span className={styles.code}>404</span>
      <h1 className={styles.title}>Такой страницы нет</h1>
      <p className={`${uiStyles.muted} ${styles.text}`}>
        Возможно, ссылка устарела или в адресе опечатка.{isSignedIn && ' Ваши задачи и расписание на месте.'}
      </p>
      <div className={styles.actions}>
        <Link to="/" className={`${uiStyles.button} ${uiStyles.primary}`}>
          <Icon name="grid" size={16} />
          {isSignedIn ? 'На дашборд' : 'На главную'}
        </Link>
        {isSignedIn && (
          <Link to="/schedule" className={`${uiStyles.button} ${uiStyles.outline}`}>
            К расписанию
          </Link>
        )}
      </div>
    </div>
  )
}

export function NotFoundPage() {
  const { data: profile } = useProfile()
  if (!profile) return <NotFound isSignedIn={false} />

  return (
    <AppLayout>
      <NotFound isSignedIn />
    </AppLayout>
  )
}
