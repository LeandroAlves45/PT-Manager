import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';

import { isApiProblem } from '@/shared/api/problem';

/**
 * Cria o cliente do TanStack Query com a política de retry do projecto.
 *
 * Um 4xx não melhora por insistir: são erros de contrato, de permissão ou de estado.
 * Falhas de rede e 5xx de leitura, sim — até duas tentativas.
 */
function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: (failureCount, error) => {
          if (isApiProblem(error) && error.status < 500) return false;

          return failureCount < 2;
        },
      },
      mutations: {
        retry: false,
      },
    },
  });
}

/** Disponibiliza o cache de dados do servidor a toda a aplicação. */
export function QueryProvider({ children }: { children: ReactNode }) {
  // 'useState' com inicializador para o cliente não ser recriado a cada render.
  const [queryClient] = useState(createQueryClient);

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
