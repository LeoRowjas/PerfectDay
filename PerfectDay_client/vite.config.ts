import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Фронт и API работают с одного адреса, поэтому httpOnly-cookie с SameSite=Strict доходит до сервера.
    proxy: {
      '/api': process.env.API_TARGET ?? 'http://localhost:5244',
    },
  },
  test: {
    environment: 'node',
  },
})
