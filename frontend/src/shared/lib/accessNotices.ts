import { platformEnforcementReasonLabel } from '@/shared/lib/platformEnforcement';

/**
 * Códigos de domínio com que a API corta o acesso de uma conta e o texto que o utilizador vê
 * na página de entrada. O personal trainer bloqueado vê o motivo e a nota; o cliente fora do
 * prazo ou arquivado nunca vê motivo nem explicação.
 */

export const SESSION_ENDED_CODE = 'authentication_session_ended';
export const TRAINER_BLOCKED_CODE = 'trainer_account_blocked';
export const CLIENT_ACCESS_ENDED_CODE = 'client_access_period_ended';

export const SESSION_ENDED_MESSAGE = 'A tua sessão terminou. Entra novamente.';
export const CLIENT_ACCESS_ENDED_MESSAGE = 'O prazo de acesso terminou.';

/**
 * Mensagem de uma conta de personal trainer bloqueada: o motivo (rótulo PT) e, se existir,
 * a nota.
 *
 * @param reason Código do motivo vindo de `block_reason`.
 * @param note Nota livre vinda de `block_note`.
 */
export function trainerBlockedMessage(reason: string | null, note: string | null): string {
  const base = 'A tua conta está bloqueada.';
  // Um código que a app não conhece não mostra «Motivo: —»: fica só a frase base.
  const label = reason === null ? null : platformEnforcementReasonLabel(reason);
  const withReason = label === null || label === '—' ? base : `${base} Motivo: ${label}.`;
  const trimmed = note?.trim() ?? '';
  return trimmed.length > 0 ? `${withReason} Nota: ${trimmed}` : withReason;
}

/**
 * Texto de aviso para um código de corte de acesso, ou `null` quando o código não é um deles.
 *
 * @param code `title` do ProblemDetails.
 * @param reason Extensão `block_reason`.
 * @param note Extensão `block_note`.
 */
export function accessCutMessage(
  code: string,
  reason: string | null,
  note: string | null
): string | null {
  switch (code) {
    case SESSION_ENDED_CODE:
      return SESSION_ENDED_MESSAGE;
    case TRAINER_BLOCKED_CODE:
      return trainerBlockedMessage(reason, note);
    case CLIENT_ACCESS_ENDED_CODE:
      return CLIENT_ACCESS_ENDED_MESSAGE;
    default:
      return null;
  }
}
