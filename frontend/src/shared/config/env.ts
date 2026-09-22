import { invariant } from '@/shared/lib/invariant';

/**
 * Variáveis de ambiente validadas uma única vez, no arranque.
 *
 * Falhar aqui é deliberado: uma base de API em falta produziria centenas de pedidos para
 * `undefined/api/v1/...` e um ecrã de erro incompreensível. Melhor rebentar com uma
 * mensagem que diz exactamente o que falta no `.env`.
 */

function required(name: string, value: string | undefined): string {
  invariant(
    value !== undefined && value.length > 0,
    `A variável de ambiente ${name} não está definida. Analisa as variáveis de ambiente do projeto.`
  );
  return value;
}

const apiBaseUrl = required('VITE_API_BASE_URL', import.meta.env.VITE_API_BASE_URL);

invariant(
  apiBaseUrl.startsWith('https://'),
  'VITE_API_BASE_URL tem de ser HTTPS: o cookie de refresh usa o prefixo __Secure- e o CORS do ' +
  'backend recusa origens inseguras.'
);

export const env = {
  apiBaseUrl: apiBaseUrl.replace(/\/$/, ''),
  apiPrefix: '/api/v1',
  isDevelopment: import.meta.env.DEV,
} as const;
