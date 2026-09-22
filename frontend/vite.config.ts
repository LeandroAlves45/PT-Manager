/// <reference types="vitest/config" />
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
export default defineConfig({
  plugins: [react(), tailwindcss(), basicSsl()],
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
    env: { VITE_API_BASE_URL: 'https://localhost:7186' },
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    css: false,
    coverage: {
      provider: 'v8',
      reportsDirectory: './coverage',
      exclude: ['src/shared/api/schema.d.ts', 'src/test/**', '**/*.config.'],
    },
  },
});
