import { Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { useProfile } from './api/queries'
import { AppLayout } from './components/AppLayout'
import { FormError, Loading } from './components/ui'
import { AnalyticsPage } from './features/analytics/AnalyticsPage'
import { NotFoundPage } from './components/NotFoundPage'
import { LoginPage, RegisterPage } from './features/auth/AuthPage'
import { DashboardPage } from './features/dashboard/DashboardPage'
import { ProfilePage } from './features/profile/ProfilePage'
import { SchedulePage } from './features/schedule/SchedulePage'
import { TasksPage } from './features/tasks/TasksPage'

function SessionGate({ requireAuth }: { requireAuth: boolean }) {
  const { data: profile, isPending, error } = useProfile()

  if (isPending) return <Loading />
  if (error) return <FormError>{error.message}</FormError>

  if (requireAuth && !profile) return <Navigate to="/login" replace />
  if (!requireAuth && profile) {
    // Пока не заполнен профиль, первый экран — онбординг.
    return <Navigate to={profile.birthDate && profile.chronotype ? '/' : '/profile'} replace />
  }

  return <Outlet />
}

export function App() {
  return (
    <Routes>
      <Route element={<SessionGate requireAuth={false} />}>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Route>
      <Route element={<SessionGate requireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="/tasks" element={<TasksPage />} />
          <Route path="/schedule" element={<SchedulePage />} />
          <Route path="/analytics" element={<AnalyticsPage />} />
          <Route path="/profile" element={<ProfilePage />} />
        </Route>
      </Route>
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
