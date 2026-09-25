import { differenceInYears, parseISO } from 'date-fns';

import { isApiProblem } from '@/shared/api/problem';

/**
 * Rótulos PT-PT dos valores que a API devolve em inglês, numa só fonte.
 *
 * Lista, detalhe e formulários importam daqui.
 */

/** Sexo biológico aceite pelo backend (`client_sex_invalid` para outro valor). */
export const SEX_LABELS = { male: 'Masculino', female: 'Feminino' } as const;

/** Níveis de atividade de 'ActivityLevel.FromString'. */
export const ACTIVITY_LEVEL_LABELS = {
  sedentary: 'Sedentário',
  lightly_active: 'Ligeiramente ativo',
  moderately_active: 'Moderadamente ativo',
  very_active: 'Muito ativo',
  extremely_active: 'Extremamente ativo',
} as const;

/**
 * Níveis de condição física sugeridos. O backend aceita texto livre até 50 caracteres; a
 * UI oferece estes três e preserva um valor antigo diferente (ver `fitnessLevelOptions`).
 */
export const FITNESS_LEVEL_LABELS = {
  beginner: 'Iniciante',
  intermediate: 'Intermédio',
  advanced: 'Avançado',
} as const;

/** Rótulo de um valor conhecido, ou o próprio valor quando o mapa não o conhece. */
export function labelFor(labels: Readonly<Record<string, string>>, value: string): string {
  return labels[value] ?? value;
}

/** Opções do nível de condição física, incluindo um valor guardado fora da lista. */
export function fitnessLevelOptions(current: string): [string, string][] {
  const options = Object.entries(FITNESS_LEVEL_LABELS) as [string, string][];
  return current !== '' && !(current in FITNESS_LEVEL_LABELS)
    ? [...options, [current, current]]
    : options;
}

/** Idade em anos completos a partir de 'birth_date' ('yyyy-MM-dd'). */
export function ageFrom(birthDate: string, today: Date = new Date()): number {
  return differenceInYears(today, parseISO(birthDate));
}

/** Iniciais para o avatar: primeira letra do primeiro e do último nome. */
export function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/);
  const first = parts[0]?.[0] ?? '';
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
  return `${first}${last}`.toUpperCase();
}

/**
 * Recusas de subscrição e de relação ao criar ou reativar um cliente (`ClientErrors`).
 * Criar e reativar partilham o mesmo mapa: é a mesma verificação de capacidade no backend.
 */
export const CLIENT_CAPACITY_MESSAGES: Readonly<Record<string, string>> = {
  client_limit_reached: 'Atingiste o limite de clientes do teu plano.',
  subscription_inactive: 'A subscrição não está ativa.',
  subscription_suspended: 'A subscrição está suspensa.',
  subscription_cancelled: 'A subscrição foi cancelada.',
  client_user_already_has_active_relationship:
    'A conta deste cliente já está ligada a outro personal trainer.',
};

/** Mensagem do toast quando arquivar ou reativar falha. */
export function activityErrorMessage(error: unknown): string {
  return (
    (isApiProblem(error) ? CLIENT_CAPACITY_MESSAGES[error.code] : undefined) ??
    'Não foi possível concluir a ação. Tenta novamente.'
  );
}
