import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The .NET API runs on http://localhost:5080 (src/Gaia.Api/Properties/launchSettings.json).
// In development Vite forwards every /api call to it, so the browser sees a single origin (no CORS).
const apiUrl = process.env.GAIA_API_URL ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': apiUrl,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.js',
    include: ['src/**/*.test.{js,jsx}'],
  },
})
