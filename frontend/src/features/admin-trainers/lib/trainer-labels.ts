import { formatLisbonDateTime } from '@/shared/lib/format';
import { isApiProblem } from '@/shared/api/problem';

/**
 * Textos PT-PT da gestão de treinadores do superuser.
 *
 * Os valores de `subscription_status` e `subscription_tier` são cadeias abertas no contrato;
 * um valor desconhecido mostra-se tal como chega, em vez de esconder um estado novo.
 */

const STATUS_LABELS: Readonly<Record<string, string>> = {
  ACTIVE: 'Ativa',
  SUSPENDED: 'Suspensa por pagamento',
  INACTIVE: 'Inativa',
  CANCELLED: 'Cancelada',
};

const TIER_LABELS: Readonly<Record<string, string>> = {
  FREE: 'Free',
  STARTER: 'Starter',
  PRO: 'Pro',
};

export function subscriptionStatusLabel(status: string): string {
  return Object.hasOwn(STATUS_LABELS, status) ? (STATUS_LABELS[status] ?? status) : status;
}

export function subscriptionTierLabel(tier: string): string {
  return Object.hasOwn(TIER_LABELS, tier) ? (TIER_LABELS[tier] ?? tier) : tier;
}

export const INVALID_EMAIL_MESSAGE = 'Escreve um email completo e válido.';
export const STRIPE_UNAVAILABLE_MESSAGE =
  'Não foi possível cancelar a subscrição no Stripe. Tenta novamente.';
export const EMAIL_DELIVERY_MESSAGE = 'Não foi possível enviar o email. Tenta novamente.';
export const EMAIL_REGISTERED_MESSAGE =
  'Este email já tem conta. Procura-o em Treinadores e isenta a conta.';
export const GENERIC_ERROR_MESSAGE = 'Não foi possível concluir a ação. Tenta novamente.';

/** O convite já passou do prazo (RN-018: vale 7 dias). */
export function isInvitationExpired(expiresAt: string, now: Date = new Date()): boolean {
  return new Date(expiresAt).getTime() < now.getTime();
}

/** Mensagem de uma falha ao enviar um convite. */
export function sendInvitationMessage(error: unknown): string {
  if (!isApiProblem(error)) return GENERIC_ERROR_MESSAGE;
  if (error.code === 'trainer_invitation_email_registered') return EMAIL_REGISTERED_MESSAGE;
  if (error.code === 'authentication_email_delivery_unavailable') return EMAIL_DELIVERY_MESSAGE;
  if (error.status === 400) return INVALID_EMAIL_MESSAGE;
  return GENERIC_ERROR_MESSAGE;
}

/** «Válido até 14/09/2026, 10:00» ou, passado o prazo, «Expirou a 14/09/2026, 10:00» (Lisboa). */
export function invitationValidityText(expiresAt: string): string {
  const when = formatLisbonDateTime(expiresAt);
  return isInvitationExpired(expiresAt) ? `Expirou a ${when}` : `Válido até ${when}`;
}
