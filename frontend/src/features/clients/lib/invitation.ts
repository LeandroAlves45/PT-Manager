/** Fuso por omissão enquanto as definições do personal trainer não chegam. */
const DEFAULT_TIME_ZONE = 'Europe/Lisbon';

const FORMAT_OPTIONS: Intl.DateTimeFormatOptions = {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
};

/**
 * Instante do convite no fuso do personal trainer: «21/10/2026 às 14:30».
 *
 * O backend envia UTC; o dia e a hora que o personal trainer vê são os do fuso das suas definições,
 * não os do browser. Um fuso inválido cai no de Lisboa em vez de rebentar o ecrã.
 */
export function formatInviteMoment(value: string, timeZone: string | undefined): string {
  const date = new Date(value);
  let parts: Intl.DateTimeFormatPart[];
  try {
    parts = new Intl.DateTimeFormat('pt-PT', {
      ...FORMAT_OPTIONS,
      timeZone: timeZone ?? DEFAULT_TIME_ZONE,
    }).formatToParts(date);
  } catch {
    parts = new Intl.DateTimeFormat('pt-PT', {
      ...FORMAT_OPTIONS,
      timeZone: DEFAULT_TIME_ZONE,
    }).formatToParts(date);
  }
  const get = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((part) => part.type === type)?.value ?? '';

  return `${get('day')}/${get('month')}/${get('year')} às ${get('hour')}:${get('minute')}`;
}

/**
 * Texto do estado do convite na ficha; `null` quando não há nada a dizer (`none`, `accepted`).
 *
 * Quando o email do convite já não coincide com o `contact_email` atual da ficha, diz para
 * que email o convite foi, para o personal trainer ver a diferença.
 */
export function invitationNote(
  invitation: { state: string; email: string | null; expires_at: string | null } | undefined,
  contactEmail: string | null,
  timeZone: string | undefined
): string | null {
  if (invitation === undefined || invitation.expires_at === null) return null;
  if (invitation.state !== 'pending' && invitation.state !== 'expired') return null;

  const moment = formatInviteMoment(invitation.expires_at, timeZone);
  const differs =
    invitation.email !== null &&
    invitation.email.trim().toLowerCase() !== (contactEmail ?? '').trim().toLowerCase();

  if (invitation.state === 'pending')
    return differs
      ? `Convite enviado para ${invitation.email} · válido até ${moment}`
      : `Convite enviado · válido até ${moment}`;

  return differs
    ? `O convite expirou a ${moment} · foi enviado para ${invitation.email}`
    : `O convite expirou a ${moment}`;
}
