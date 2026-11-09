import { defineConfig, devices } from '@playwright/test'

// E2E tests drive the real app on their OWN ports (API 5081, Vite 5174), so they never reuse
// a development server a developer may already have running on 5080/5173.
// --no-launch-profile stops launchSettings.json from overriding ASPNETCORE_ENVIRONMENT, so the
// API really runs as "E2E" (fake weather provider, its own database).
const apiUrl = 'http://localhost:5081'
const webUrl = 'http://localhost:5174'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: webUrl,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'dotnet run --project ../src/Gaia.Api --no-launch-profile',
      url: `${apiUrl}/health`,
      env: { ASPNETCORE_ENVIRONMENT: 'E2E', ASPNETCORE_URLS: apiUrl },
      reuseExistingServer: false,
      timeout: 120_000,
    },
    {
      command: 'npm run dev -- --port 5174',
      url: webUrl,
      env: { GAIA_API_URL: apiUrl },
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
})
