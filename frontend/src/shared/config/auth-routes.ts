/**
 * Rotas públicas de autenticação.
 *
 * Num só sítio porque são referidas pelos guards, pelo menu de perfil, pelo router e pelos
 * links entre os próprios ecrãs de autenticação: um caminho escrito à mão em cada um deles
 * é um caminho que falha em silêncio quando muda (o `*` do router engole rotas erradas).
 */
export const AUTH_ROUTES = {
  login: '/entrar',
  signUp: '/criar-conta',
  forgotPassword: '/recuperar-password',
  resetPassword: '/repor-password',
  confirmEmail: '/confirmar-email',
  acceptInvite: '/aceitar-convite',
  resendConfirmation: '/reenviar-confirmacao',
} as const;

/**
 * Rotas antigas, que continuam a redirecionar para `login`.
 *
 * Favoritos e emails já enviados apontam para elas.
 */
export const LEGACY_LOGIN_ROUTES = ['/auth', '/auth/login'] as const;

/**
 * Monta um caminho com o token numa query string.
 *
 * O token de um link de email chega sempre por query string (é o que o email transporta), mas
 * só os tokens: emails e outros dados pessoais viajam em `location.state`, nunca no URL.
 */
export function withToken(route: string, token: string): string {
  return `${route}?token=${encodeURIComponent(token)}`;
}
