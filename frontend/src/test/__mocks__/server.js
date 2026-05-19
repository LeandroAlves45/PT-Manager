/**
 * server.js — instância do servidor MSW para os testes.
 *
 * setupServer() cria um servidor de interceptação HTTP que funciona
 * no ambiente Node.js (jsdom). É diferente do service worker usado
 * em desenvolvimento no browser — ambos partilham os mesmos handlers.
 *
 * Esta instância é usada no setup.js:
 *   beforeAll(() => server.listen())
 *   afterEach(() => server.resetHandlers())
 *   afterAll(() => server.close())
 */

import { setupServer } from 'msw/node';
import { handlers } from './handlers';

// Cria o servidor MSW com os handlers definidos
export const server = setupServer(...handlers);
