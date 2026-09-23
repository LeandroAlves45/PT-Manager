import { setupServer } from 'msw/node';

import { handlers } from '@/test/msw/handlers';

/** Instância única do MSW para a suite; o ciclo de vida pertence a `setup.ts`. */
export const server = setupServer(...handlers);
