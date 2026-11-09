import { NavLink, Outlet } from 'react-router-dom'
import { useLogout, useProfile } from '../api/queries'
import dashboardActive from '../assets/nav-dashboard-active.svg'
import dashboardInactive from '../assets/nav-dashboard-inactive.svg'
import profileActive from '../assets/nav-profile-active.svg'
import profileInactive from '../assets/nav-profile-inactive.svg'
import scheduleActive from '../assets/nav-schedule-active.svg'
import scheduleInactive from '../assets/nav-schedule-inactive.svg'
import tasksActive from '../assets/nav-tasks-active.svg'
import tasksInactive from '../assets/nav-tasks-inactive.svg'
import logo from '../assets/logo.svg'
import styles from './AppLayout.module.css'

const navigation = [
  { to: '/', label: 'Дашборд', active: dashboardActive, inactive: dashboardInactive },
  { to: '/tasks', label: 'Задачи', active: tasksActive, inactive: tasksInactive },
  { to: '/schedule', label: 'Расписание', active: scheduleActive, inactive: scheduleInactive },
  { to: '/profile', label: 'Профиль', active: profileActive, inactive: profileInactive },
]

export function AppLayout() {
  const { data: profile } = useProfile()
  const logout = useLogout()

  return (
    <div className={styles.layout}>
      <aside className={styles.sidebar}>
        <div className={styles.logo}>
          <img src={logo} alt="" width={28} height={28} />
          <span>Идеальный День</span>
        </div>
        <nav className={styles.nav} aria-label="Основное меню">
          {navigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) => `${styles.link} ${isActive ? styles.linkActive : ''}`}
            >
              {({ isActive }) => (
                <>
                  <img src={isActive ? item.active : item.inactive} alt="" width={20} height={20} />
                  {item.label}
                </>
              )}
            </NavLink>
          ))}
        </nav>
        <div className={styles.account}>
          <span className={styles.email}>{profile?.email}</span>
          <button type="button" className={styles.logout} disabled={logout.isPending} onClick={() => logout.mutate()}>
            Выйти
          </button>
        </div>
      </aside>
      <main className={styles.content}>
        <Outlet />
      </main>
    </div>
  )
}
