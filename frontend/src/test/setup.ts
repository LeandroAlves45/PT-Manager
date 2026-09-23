import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterAll, afterEach, beforeAll, beforeEach, vi } from 'vitest';

import { clearSession } from '@/shared/api/session';
import { setViewport } from '@/test/browser-fakes';
import { server } from '@/test/msw/server';

// O primeiro render de uma árvore com o AppShell completo passa de 1 s em máquinas lentas;
// o limite só afeta testes que falhariam de qualquer forma.
configure({ asyncUtilTimeout: 5000 });

// Radix (Progress, Dialog) e cmdk medem e fazem scroll de elementos; o jsdom não tem layout,
// por isso bastam implementações vazias.
globalThis.ResizeObserver ??= class {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
};
Object.defineProperty(Element.prototype, 'scrollIntoView', {
  configurable: true,
  value: () => undefined,
});

// `error`: um pedido sem handler é um teste que toca a rede real ou um endpoint esquecido.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

// Telemóvel por omissão: é o layout mobile-first. Um teste de desktop chama `setViewport`.
beforeEach(() => setViewport(375));

afterEach(() => {
  cleanup();
  // Menus modais do Radix põem `pointer-events: none` no body e o unmount do teste nem
  // sempre o repõe; o teste seguinte deixaria de conseguir clicar.
  document.body.removeAttribute('style');
  server.resetHandlers();
  clearSession();
  // Limpeza entre testes (tema guardado pelo ThemeProvider), não dados de sessão.
  // eslint-disable-next-line no-restricted-globals
  sessionStorage.clear();
  // eslint-disable-next-line no-restricted-globals
  localStorage.clear();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

afterAll(() => server.close());
