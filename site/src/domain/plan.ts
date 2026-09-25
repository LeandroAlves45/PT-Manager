import type { Plan } from '@/domain/types';

/** Texto do limite de clientes de um plano, derivado de `clientLimit` (`null` = ilimitado). */
export function clientLimitLabel(plan: Plan): string {
  return plan.clientLimit === null ? 'Clientes ilimitados' : `Até ${plan.clientLimit} clientes`;
}

/** Nota curta do plano gratuito para o hero, derivada dos dados (nunca escrita à mão). */
export function freePlanNote(plans: readonly Plan[]): string {
  const free = plans.find((plan) => plan.monthlyPriceEur === 0);
  if (!free || free.clientLimit === null) return '';
  return `Plano ${free.code === 'FREE' ? 'Free' : free.code} até ${free.clientLimit} clientes, sem cartão.`;
}
