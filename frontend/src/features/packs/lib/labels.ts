import type { components } from '@/shared/api/schema';
import { formatCurrency } from '@/shared/lib/format';

type ClientPack = components['schemas']['ClientSessionPackResponse'];

/**
 * Rótulos e mensagens da feature packs, numa só fonte ('PackErrors', 'ClientErrors').
 */

/** Recusas de "Atribuir pack" que não pertencem a um campo. */
export const ASSIGN_PACK_ERRORS: Readonly<Record<string, string>> = {
  client_not_found: 'Este cliente já não existe.',
  client_inactive: 'Este cliente está arquivado. Reativa-o antes de lhe atribuir um pack.',
  pack_type_not_found: 'Este tipo de pack já não existe.',
  pack_type_inactive: 'Este tipo de pack foi arquivado. Escolhe outro pack.',
};

/** Recusas de "Cancelar pack". */
export const CANCEL_PACK_ERRORS: Readonly<Record<string, string>> = {
  client_session_pack_used: 'Este pack já foi usado e não pode ser cancelado.',
  client_session_pack_referenced:
    'Há sessões associadas a este pack. Tira-as do pack antes de o cancelar.',
};

/** Saldo em texto: "3 de 10 restantes". */
export function balanceLabel(pack: Pick<ClientPack, 'sessions_remaining' | 'sessions_total'>) {
  return `${pack.sessions_remaining} de ${pack.sessions_total} ${
    pack.sessions_remaining === 1 ? 'restante' : 'restantes'
  }`;
}

/** Preço de um tipo de pack: "300,00€". */
export function priceLabel(item: { price_cents: number; currency: string }) {
  return formatCurrency(item.price_cents, item.currency);
}
