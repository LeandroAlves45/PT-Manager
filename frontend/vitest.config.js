/**
 * vitest.config.js — configuração de testes para o PT Manager Frontend.
 *
 * Vitest partilha a configuração do Vite — importa o mesmo vite.config.js.
 * Não é necessário configurar Babel nem transformers de JSX separados.
 *
 * Ambiente jsdom: simula o browser (window, document, localStorage, etc.)
 * sem precisar de um browser real. É o ambiente correcto para React.
 *
 * globals: true — permite usar describe(), test(), expect() sem import,
 * exactamente como Jest. Mais conveniente e consistente com os exemplos online.
 */

import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],

  test: {
    // Sobrepõe VITE_API_BASE_URL para apontar ao servidor de testes local
    // (o .env define a URL de produção que não deve ser usada em testes)
    env: {
      VITE_API_BASE_URL: 'http://localhost:8000',
    },

    // Ambiente que simula o browser - necessário para testar componentes React
    environment: 'jsdom',

    // Permite usar describe(), test(), expect() sem import
    globals: true,

    // Ficheiro de setup executado antes de cada ficheiro de teste
    setupFiles: ['./src/test/setup.js'],

    // Inclui todos os ficheiros
    include: ['src/**/*.{test,spec}.{js,jsx}'],

    // Exclui node_modules e dist
    exclude: ['node_modules', 'dist'],

    // CSS: transform imports de CSS em mocks
    css: false,
  },

  // Path aliases para importações mais limpas
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
});
