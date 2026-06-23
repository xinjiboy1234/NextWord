import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  timeout: 60_000,
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
    ...devices['Desktop Chrome'],
  },
  webServer: [
    {
      command: 'dotnet run --project Backend/NextWord.Api/NextWord.Api.csproj --launch-profile http',
      url: 'http://localhost:5108/api/health',
      cwd: '..',
      name: 'API',
      reuseExistingServer: false,
      timeout: 180_000,
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:5173',
      name: 'Frontend',
      reuseExistingServer: false,
      timeout: 60_000,
      dependencies: ['API'],
    },
  ],
})
