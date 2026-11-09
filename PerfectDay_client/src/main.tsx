import '@fontsource-variable/manrope'
import './styles/global.css'
import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { ApiError } from './api/client'
import { queryKeys, setSession } from './api/queries'
import { App } from './App'

// Сессия истекла: сбрасываем данные и отправляем на страницу входа.
function handleUnauthorized(error: Error) {
  if (error instanceof ApiError && error.status === 401 && queryClient.getQueryData(queryKeys.profile)) {
    setSession(queryClient, null)
  }
}

const isNotFound = (error: Error) => error instanceof ApiError && error.status === 404

const queryClient: QueryClient = new QueryClient({
  queryCache: new QueryCache({
    onError: (error) => {
      handleUnauthorized(error)
      // Возможно, удалён сам аккаунт: проверка профиля отправит на страницу входа.
      if (isNotFound(error)) void queryClient.invalidateQueries({ queryKey: queryKeys.profile })
    },
  }),
  mutationCache: new MutationCache({
    onError: (error) => {
      handleUnauthorized(error)
      // Объект уже изменился, например в другой вкладке: на экране устаревшие данные.
      if (isNotFound(error)) void queryClient.invalidateQueries()
    },
  }),
  defaultOptions: {
    queries: {
      retry: false,
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
