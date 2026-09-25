import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { Tooltip as TooltipPrimitive } from 'radix-ui';
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router';

import { AuthProvider } from '@/app/providers/AuthProvider';
import { ThemeProvider } from '@/app/providers/ThemeProvider';
import { routes as applicationRoutes } from '@/app/router';

/** `QueryClient` novo por teste, sem retries: um erro tem de aparecer já, não à 3.ª tentativa. */
export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

type InitialEntry = string | { pathname: string; search?: string; state?: unknown };

/**
 * Monta a aplicação com a mesma hierarquia de `main.tsx` (`ThemeProvider`,
 * `QueryClientProvider`, `AuthProvider`, `Tooltip.Provider` por fora do router) sobre um router em memória.
 *
 * Por omissão usa as rotas reais de `app/router.tsx`, incluindo `AppRouteRoot` com o
 * `NuqsAdapter`, para que guards, layouts e redirecionamentos sejam os de produção.
 */
export function renderApp({
  initialEntries = ['/'],
  routes = applicationRoutes,
  queryClient = createTestQueryClient(),
}: {
  initialEntries?: InitialEntry[];
  routes?: RouteObject[];
  queryClient?: QueryClient;
} = {}) {
  // [6E] NOVO: o adapter do nuqs lê a query string de `window.location`, não do router em
  // memória. Sem isto, `?new=true` ou `?activity=all` nunca chegavam ao ecrã, e o URL de um
  // teste anterior (escrito pelo nuqs com `history.replaceState`) contaminava o seguinte.
  const current = initialEntries[initialEntries.length - 1] ?? '/';
  window.history.replaceState(
    null,
    '',
    typeof current === 'string' ? current : `${current.pathname}${current.search ?? ''}`
  );

  const router = createMemoryRouter(routes, { initialEntries });

  const result = render(
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <TooltipPrimitive.Provider>
            <RouterProvider router={router} />
          </TooltipPrimitive.Provider>
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  );

  return { ...result, router, queryClient };
}
