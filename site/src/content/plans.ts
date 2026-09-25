import type { Plan } from '@/domain/types';

/**
 * Planos à venda.
 *
 * Os limites de clientes têm de coincidir com `SubscriptionTier` no backend
 * (`backend/src/Domain/ValueObjects/SubscriptionTier.cs`); um teste falha se divergirem.
 * Os preços (20 €/40 €) vivem no dashboard Stripe, fora do repo: confirmados pelo utilizador
 * em 2026-09-25. Mudar lá obriga a mudar aqui.
 */
export const plans: readonly Plan[] = [
  {
    code: 'FREE',
    monthlyPriceEur: 0,
    clientLimit: 5,
    priceNote: 'para sempre',
    benefits: ['Funcionalidades essenciais', 'Marca própria no portal do cliente'],
    highlighted: false,
  },
  {
    code: 'STARTER',
    monthlyPriceEur: 20,
    clientLimit: 25,
    priceNote: '/mês',
    benefits: ['Tudo do Free'],
    highlighted: false,
  },
  {
    code: 'PRO',
    monthlyPriceEur: 40,
    clientLimit: null,
    priceNote: '/mês',
    benefits: ['Tudo do Starter'],
    highlighted: true,
    badge: 'Mais popular',
  },
];
