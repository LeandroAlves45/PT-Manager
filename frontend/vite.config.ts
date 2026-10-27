/// <reference types="vitest/config" />
import { rmSync } from 'node:fs';
import { fileURLToPath, URL } from 'node:url';

import basicSsl from '@vitejs/plugin-basic-ssl';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

/**
 * Configuração do Vite.
 *
 * HTTPS local é obrigatório, não uma preferência: o cookie de refresh usa o prefixo
 * `__Secure-`, que o browser só aceita em ligações seguras, e `ApiCorsOptions` recusa
 * origens que não sejam HTTPS. Sem isto a sessão nunca chega a ser restaurada.
 *
 * A porta 5173 é fixa (`strictPort`) porque a origem tem de coincidir exactamente com
 * `Cors:AllowedOrigins` dos User Secrets de Development do backend.
 */
/**
 * O `public/mockServiceWorker.js` é gerado pelo MSW para o arnês visual e para o
 * desenvolvimento. Não faz parte do produto: sai do `dist` para não ser servido em produção.
 */
const dropMockServiceWorker = () => ({
  name: 'drop-mock-service-worker',
  apply: 'build' as const,
  closeBundle() {
    rmSync(fileURLToPath(new URL('./dist/mockServiceWorker.js', import.meta.url)), { force: true });
  },
});

export default defineConfig({
  plugins: [react(), tailwindcss(), basicSsl(), dropMockServiceWorker()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
  },
  preview: {
    port: 5173,
    strictPort: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    // Fixa a origem da API nos testes para os handlers do MSW não dependerem do `.env`
    // da máquina de quem corre a suite.
    // O fuso também é fixo: o CI corre em UTC, onde "offset local" e "Z" coincidem e um
    // instante enviado sem o offset do browser passaria nos testes. Lisboa tem +01:00 no verão.
    env: {
      VITE_API_BASE_URL: 'https://localhost:7186',
      TZ: 'Europe/Lisbon',
      VITE_GOOGLE_CLIENT_ID: '',
    },
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    // Igual ao gate 6E-4 (`--maxWorkers=4 --testTimeout=15000`). O setup espera até
    // 5000 ms; o teste tem de viver mais do que isso, senão o Vitest esconde o erro
    // da Testing Library. Um worker por ficheiro, nesta máquina, nem chega a arrancar.
    testTimeout: 15_000,
    hookTimeout: 15_000,
    maxWorkers: 4,
    css: false,
    coverage: {
      provider: 'v8',
      reportsDirectory: './coverage',
      exclude: ['src/shared/api/schema.d.ts', 'src/test/**', '**/*.config.*'],
      // Medido no CI #61: linhas 90.7, instruções 89.2, funções 88.1, ramos 82.0. Os limiares
      // ficam 2 pontos abaixo para apanhar regressões reais sem falhar por ruído.
      thresholds: { lines: 88, statements: 87, functions: 86, branches: 80 },
    },
  },
});
