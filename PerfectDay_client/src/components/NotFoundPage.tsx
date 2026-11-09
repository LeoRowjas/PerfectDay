import { Link } from 'react-router-dom'
import logo from '../assets/logo.svg'
import styles from './NotFoundPage.module.css'
import uiStyles from './ui.module.css'

export function NotFoundPage() {
  return (
    <main className={styles.page}>
      <img src={logo} alt="" width={56} height={56} />
      <h1 className={styles.title}>Такой страницы нет</h1>
      <p className={uiStyles.muted}>Возможно, в адресе опечатка или страница была перемещена.</p>
      <Link to="/" className={`${uiStyles.button} ${uiStyles.primary}`}>
        Вернуться к плану дня
      </Link>
    </main>
  )
}
