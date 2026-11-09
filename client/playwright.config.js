import { defineConfig, devices } from '@playwright/test'

// E2E tests drive the real app: the .NET API (in the "E2E" environment, which uses the fake
// weather provider and its own database) plus the Vite dev server that proxies /api to it.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'dotnet run --project ../src/Gaia.Api --launch-profile http',
      url: 'http://localhost:5080/health',
      env: { ASPNETCORE_ENVIRONMENT: 'E2E' },
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:5173',
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
})
