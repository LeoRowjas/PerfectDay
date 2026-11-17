import type { ReactNode } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import analyticsActive from '../assets/nav-analytics-active.svg'
import analyticsInactive from '../assets/nav-analytics-inactive.svg'
import dashboardActive from '../assets/nav-dashboard-active.svg'
import dashboardInactive from '../assets/nav-dashboard-inactive.svg'
import profileActive from '../assets/nav-profile-active.svg'
import profileInactive from '../assets/nav-profile-inactive.svg'
import scheduleActive from '../assets/nav-schedule-active.svg'
import scheduleInactive from '../assets/nav-schedule-inactive.svg'
import tasksActive from '../assets/nav-tasks-active.svg'
import tasksInactive from '../assets/nav-tasks-inactive.svg'
import logo from '../assets/logo.svg'
import { NotificationToast } from './NotificationCenter'
import styles from './AppLayout.module.css'

const navigation = [
  { to: '/', label: 'Дашборд', active: dashboardActive, inactive: dashboardInactive },
  { to: '/tasks', label: 'Задачи', active: tasksActive, inactive: tasksInactive },
  { to: '/schedule', label: 'Расписание', active: scheduleActive, inactive: scheduleInactive },
  { to: '/profile', label: 'Профиль', active: profileActive, inactive: profileInactive },
  { to: '/analytics', label: 'Аналитика', active: analyticsActive, inactive: analyticsInactive },
]

function Navigation({ className, linkClass, activeClass }: { className: string; linkClass: string; activeClass: string }) {
  return (
    <nav className={className} aria-label="Основное меню">
      {navigation.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.to === '/'}
          className={({ isActive }) => `${linkClass} ${isActive ? activeClass : ''}`}
        >
          {({ isActive }) => (
            <>
              <img src={isActive ? item.active : item.inactive} alt="" width={20} height={20} className={styles.navIcon} />
              <span
                aria-hidden="true"
                className={styles.navMask}
                style={{ maskImage: `url("${item.inactive}")`, WebkitMaskImage: `url("${item.inactive}")` }}
              />
              {item.label}
            </>
          )}
        </NavLink>
      ))}
    </nav>
  )
}

/** Боковое меню на компьютере и нижнее на телефоне. children нужен странице 404, у которой нет своего маршрута. */
export function AppLayout({ children }: { children?: ReactNode }) {
  return (
    <div className={styles.layout}>
      <aside className={styles.sidebar}>
        <div className={styles.logo}>
          <img src={logo} alt="" width={28} height={28} />
          <span>Идеальный День</span>
        </div>
        <Navigation className={styles.nav} linkClass={styles.link} activeClass={styles.linkActive} />
      </aside>
      <main className={styles.content}>{children ?? <Outlet />}</main>
      <Navigation className={styles.bottomNav} linkClass={styles.bottomLink} activeClass={styles.bottomLinkActive} />
      <NotificationToast />
    </div>
  )
}
