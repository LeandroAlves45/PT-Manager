import { isApiProblem, type ApiFieldError, type ApiProblem } from '@/shared/api/problem';

/**
 * Mensagens PT-PT dos erros de autenticação, num só mapa.
 *
 * Cada código existe no backend (`AuthenticationErrors`, `GoogleAuthenticationErrors`): um
 * código inventado aqui nunca chega ao ecrã. As mensagens de login e de recuperação são
 * deliberadamente vagas onde dizer mais revelaria se um email está registado.
 */

export const CONNECTION_ERROR_MESSAGE =
  'Não foi possível contactar o servidor. Verifica a ligação e tenta novamente.';
const GENERIC_MESSAGE = 'Não foi possível concluir o pedido. Tenta novamente.';
const RATE_LIMIT_MESSAGE = 'Demasiadas tentativas. Aguarda um momento e tenta novamente.';

const MESSAGES: Readonly<Record<string, string>> = {
  authentication_invalid_credentials:
    'Email ou palavra-passe inválidos. Se acabaste de criar conta, confirma primeiro o email.',
  authentication_registration_rejected:
    'Não foi possível criar a conta com estes dados. Verifica-os e tenta novamente.',
  authentication_account_inactive: 'Esta conta está desativada. Contacta o suporte.',
  authentication_email_delivery_unavailable:
    'Não foi possível enviar o email neste momento. Tenta novamente daqui a pouco.',

  authentication_confirmation_token_invalid: 'O link de confirmação não é válido.',
  authentication_confirmation_token_expired: 'O link de confirmação expirou.',
  authentication_confirmation_token_consumed:
    'Este link de confirmação já foi usado. Podes entrar com a tua conta.',

  authentication_password_reset_invalid:
    'O link para repor a palavra-passe não é válido ou já expirou.',
  authentication_new_password_rejected: 'A nova palavra-passe não é aceite. Escolhe outra.',
  authentication_current_password_invalid: 'A palavra-passe atual não está correta.',

  authentication_password_too_short: 'A palavra-passe deve ter entre 8 e 128 caracteres.',
  authentication_password_too_long: 'A palavra-passe deve ter entre 8 e 128 caracteres.',
  authentication_new_password_too_short: 'A palavra-passe deve ter entre 8 e 128 caracteres.',
  authentication_new_password_too_long: 'A palavra-passe deve ter entre 8 e 128 caracteres.',
  authentication_password_confirmation_mismatch: 'As palavras-passe não coincidem.',
  authentication_email_invalid: 'Introduz um email válido.',
  authentication_email_too_long: 'O email é demasiado longo.',
  authentication_full_name_required: 'Indica o teu nome completo.',
  authentication_full_name_too_long: 'O nome é demasiado longo.',

  authentication_invitation_invalid: 'O convite não é válido.',
  authentication_invitation_expired: 'O convite expirou. Pede um novo ao teu personal trainer.',
  authentication_invitation_consumed: 'Este convite já foi usado.',
  authentication_invitation_email_mismatch:
    'Este convite foi enviado para outro email. Entra com a conta a que o convite se destina.',
  authentication_invitation_account_exists:
    'Já existe uma conta com este email. Entra para aceitar o convite.',
  authentication_transfer_approval_required:
    'Esta conta já acompanha outro personal trainer. Confirma a mudança para aceitar o convite.',
  authentication_relationship_conflict:
    'Não foi possível associar a tua conta a este personal trainer. Contacta-o para resolver.',
  authentication_client_only: 'Só uma conta de cliente pode aceitar um convite.',

  trainer_invitation_invalid: 'O convite não é válido.',
  trainer_invitation_expired: 'Este convite expirou. Pede outro à equipa do PT Manager.',
  trainer_invitation_used: 'Este convite já foi usado. Entra com a conta que criaste.',

  google_credential_invalid: 'Não foi possível validar a conta Google. Tenta novamente.',
  google_account_not_found:
    'Não há conta com este Google. Cria uma conta ou usa o link do convite.',
  google_email_not_verified: 'O email desta conta Google não está verificado.',
  google_account_link_required:
    'Já existe uma conta com este email. Entra com a palavra-passe e liga o Google em Conta e segurança.',
  google_identity_conflict: 'Esta conta Google já está ligada a outro utilizador.',
  google_link_email_mismatch: 'A conta Google tem de ter o mesmo email que esta conta.',
  google_provider_unavailable:
    'O início de sessão com Google não está disponível agora. Tenta novamente daqui a pouco.',
};

/** Mensagem PT-PT de um código de erro, ou `undefined` quando o código não está mapeado. */
export function authCodeMessage(code: string): string | undefined {
  return Object.hasOwn(MESSAGES, code) ? MESSAGES[code] : undefined;
}

/** Mensagem PT-PT de um código, com a mensagem genérica quando o código não está mapeado. */
export function authMessage(code: string): string {
  return authCodeMessage(code) ?? GENERIC_MESSAGE;
}

/** Mensagem de erro por campo: o código mapeado ou o texto do servidor como último recurso. */
export function authFieldMessage(fieldError: ApiFieldError): string {
  return authCodeMessage(fieldError.code) ?? fieldError.message;
}

/**
 * Mensagem PT-PT para qualquer falha de um pedido de autenticação.
 *
 * Ordem: o 429 decide-se pelo estado HTTP (o `title` é "Too many requests", não um código);
 * depois o código de domínio; um 400 de validação usa o código do primeiro campo; o resto é
 * genérico. Uma falha que não seja `ApiProblem` é rede.
 */
export function authErrorMessage(error: unknown): string {
  if (!isApiProblem(error)) return CONNECTION_ERROR_MESSAGE;

  return problemMessage(error);
}

function problemMessage(problem: ApiProblem): string {
  if (problem.status === 429) return RATE_LIMIT_MESSAGE;

  const byCode = authCodeMessage(problem.code);
  if (byCode !== undefined) return byCode;

  const firstField = problem.fieldErrors[0];
  if (firstField !== undefined) {
    const byField = authCodeMessage(firstField.code);
    if (byField !== undefined) return byField;
  }

  return GENERIC_MESSAGE;
}
