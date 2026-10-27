import { defineConfig, devices } from '@playwright/test';

/**
 * E2E dos fluxos críticos contra a API real (Development, seed ligado) e a SPA compilada.
 *
 * HTTPS é obrigatório: o cookie de refresh usa o prefixo `__Secure-` e o CORS do backend recusa
 * origens inseguras. O certificado de `@vitejs/plugin-basic-ssl` e o da API de desenvolvimento
 * são auto-assinados, por isso `ignoreHTTPSErrors`.
 *
 * Um só worker, em série: os limitadores de taxa da API são por IP (todos os testes partilham
 * o mesmo) e os fluxos dependem do estado que os anteriores criam na base.
 *
 * Variáveis:
 * - `E2E_SEED_PASSWORD`: palavra-passe das contas semeadas (`DevelopmentSeed__Password`).
 * - `E2E_API_LOG`: ficheiro com o stdout da API, de onde se leem os links de email
 *   (`DevelopmentEmail__LogLinks=true`).
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: 'https://localhost:5173',
    ignoreHTTPSErrors: true,
    locale: 'pt-PT',
    timezoneId: 'Europe/Lisbon',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run preview',
    url: 'https://localhost:5173',
    ignoreHTTPSErrors: true,
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
});
