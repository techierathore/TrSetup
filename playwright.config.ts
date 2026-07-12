import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/verify',
  reporter: 'line',
  timeout: 60000,
  use: {
    baseURL: 'http://localhost:5999',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    headless: true,
  },
});
