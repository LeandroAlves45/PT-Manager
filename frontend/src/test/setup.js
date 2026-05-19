/**
 * setup.js — executado antes de CADA ficheiro de teste.
 *
 * Responsabilidades:
 *   1. Importar @testing-library/jest-dom para adicionar matchers extra ao Vitest
 *      (toBeInTheDocument, toHaveValue, toBeDisabled, etc.)
 *   2. Iniciar e limpar o servidor MSW entre testes
 *   3. Limpar localStorage entre testes (evita estado partilhado)
 *   4. Silenciar warnings esperados do React que poluem o output
 */

import '@testing-library/jest-dom';
import { server } from './__mocks__/server';

// Inicia o servidor MSW antes de todos os testes
// beforeAll: corre uma vez antes de todos os testes do ficheiro
// afterEach: limpa handlers adicionados em testes individuais
// afterAll: desliga o servidor depois de todos os testes
beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }));
afterEach(() => {
  server.resetHandlers();
  localStorage.clear();
});
afterAll(() => server.close());
