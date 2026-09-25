/// <reference types="vitest/config" />
import { readFileSync } from 'node:fs';
import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

interface VercelHeaderRule {
  source: string;
  headers: { key: string; value: string }[];
}

/**
 * Headers globais de `vercel.json`, aplicados também ao `vite preview`.
 *
 * Assim a CSP de produção é exercitada localmente: uma violação aparece na consola do
 * preview em vez de só depois do deploy. `vercel.json` é a única fonte de verdade.
 */
function productionHeaders(): Record<string, string> {
  const config = JSON.parse(readFileSync(new URL('./vercel.json', import.meta.url), 'utf8')) as {
    headers: VercelHeaderRule[];
  };
  const global = config.headers.find((rule) => rule.source === '/(.*)');
  return Object.fromEntries((global?.headers ?? []).map((h) => [h.key, h.value]));
}

/**
 * Configuração do Vite do site de marketing.
 *
 * O build é em dois passos (cliente + SSR) seguido de `scripts/prerender.mjs`, que grava o
 * HTML final: a página chega completa ao crawler sem depender de JavaScript.
 */
export default defineConfig({
  plugins: [react(), tailwindcss()],
  define: { __BUILD_YEAR__: JSON.stringify(new Date().getFullYear()) },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: { port: 5174, strictPort: true },
  preview: { port: 5174, strictPort: true, headers: productionHeaders() },
  build: {
    // Sem módulos inline: a CSP `script-src 'self'` recusaria qualquer script embutido.
    assetsInlineLimit: 0,
    modulePreload: { polyfill: false },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    env: { VITE_SITE_URL: 'https://ptmanager.pt', VITE_APP_URL: 'https://app.ptmanager.pt' },
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    css: false,
  },
});
