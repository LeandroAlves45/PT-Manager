import type { components } from '@/shared/api/schema';
import { toSession, type Session } from '@/shared/api/session';
import { saveNativeTokens } from '@/shared/native/token-store';

type NativeSessionResponse = components['schemas']['NativeSessionResponse'];

/**
 * Abre a sessão da app a partir de uma resposta de `/api/v1/native/auth/*`: guarda o refresh
 * token e o segredo anti-CSRF no armazenamento seguro e devolve a sessão em memória.
 *
 * Os tokens são guardados antes de a sessão existir: se a escrita falhar, o login falha e o
 * cliente não fica "dentro" de uma sessão que não sobrevive ao fecho da app.
 */
export async function startNativeSession(response: NativeSessionResponse): Promise<Session> {
  await saveNativeTokens({ refreshToken: response.refresh_token, csrfToken: response.csrf_token });
  return toSession(response);
}
