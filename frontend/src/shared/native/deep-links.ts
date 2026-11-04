import { AUTH_ROUTES } from '@/shared/config/auth-routes';

/** Domínio da app web: os links dos emails apontam para ele e a app abre-os. */
export const APP_LINK_HOST = 'app.ptmanager.pt';

/**
 * Caminhos que a app abre a partir de um link. Só os do cliente: o convite e a reposição da
 * palavra-passe. A confirmação de email é só de personal trainers, que usam a web.
 */
const APP_LINK_PATHS: readonly string[] = [AUTH_ROUTES.acceptInvite, AUTH_ROUTES.resetPassword];

/** Tokens dos emails: base64url, sem espaços nem outros caracteres. */
const TOKEN_PATTERN = /^[A-Za-z0-9_-]{16,512}$/;

/**
 * Converte o URL com que o sistema abriu a app (Universal Link ou App Link) numa rota interna.
 *
 * Aceita só `https://app.ptmanager.pt` num dos caminhos acima e com um único parâmetro
 * `token` bem formado. Tudo o resto devolve `null` e a app fica onde estava: um link
 * fabricado não consegue levar o cliente para outra rota nem injetar outros parâmetros.
 */
export function resolveDeepLink(rawUrl: string): string | null {
  let url: URL;
  try {
    url = new URL(rawUrl);
  } catch {
    return null;
  }

  if (url.protocol !== 'https:' || url.hostname !== APP_LINK_HOST || url.port !== '') return null;
  if (url.username !== '' || url.password !== '') return null;
  if (!APP_LINK_PATHS.includes(url.pathname)) return null;

  const keys = [...url.searchParams.keys()];
  const token = url.searchParams.get('token');
  if (keys.length !== 1 || keys[0] !== 'token' || token === null || !TOKEN_PATTERN.test(token)) {
    return null;
  }

  return `${url.pathname}?token=${token}`;
}
