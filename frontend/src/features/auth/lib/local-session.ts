import type { QueryClient } from '@tanstack/react-query';

import { clearSession } from '@/shared/api/session';
import { publishSessionInvalidated } from '@/shared/api/session-events';
import { isNativeApp } from '@/shared/native/platform';
import { clearNativeTokens } from '@/shared/native/token-store';

/**
 * Esquece a sessão neste separador (e nos outros) sem chamar o servidor.
 *
 * Usado quando o servidor já revogou as sessões (mudar ou repor a palavra-passe): um
 * `POST /auth/logout` seria só um 401. A cache de dados também sai, para a próxima conta
 * não ver nada desta. Na app nativa apaga também os tokens do armazenamento seguro, que o
 * servidor já revogou.
 */
export function endLocalSession(queryClient: QueryClient): void {
  queryClient.clear();
  clearSession();
  publishSessionInvalidated();
  if (isNativeApp()) void clearNativeTokens();
}
