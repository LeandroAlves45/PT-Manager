/**
 * Leitura segura do `location.state` entre ecrãs de autenticação.
 *
 * Dados pessoais (o email) e avisos viajam no estado do router, nunca na query string: o URL
 * fica no histórico, nos logs de proxies e no `Referer`.
 */

/** Aviso de uma operação anterior (ex.: "Palavra-passe alterada"), mostrado no login. */
export function readNotice(state: unknown): string | null {
  const notice = (state as { notice?: unknown } | null)?.notice;
  return typeof notice === 'string' && notice.length > 0 ? notice : null;
}

/** Email pré-preenchido por outro ecrã ou cadeia vazia. */
export function readEmail(state: unknown): string {
  const email = (state as { email?: unknown } | null)?.email;
  return typeof email === 'string' ? email : '';
}
