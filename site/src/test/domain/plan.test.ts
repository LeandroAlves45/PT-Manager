import { describe, expect, it } from 'vitest';

import { clientLimitLabel, freePlanNote } from '@/domain/plan';
import type { Plan } from '@/domain/types';

const base: Plan = {
  code: 'FREE',
  monthlyPriceEur: 0,
  clientLimit: 5,
  priceNote: 'para sempre',
  benefits: [],
  highlighted: false,
};

describe('clientLimitLabel', () => {
  it('mostra o limite ou "ilimitados"', () => {
    expect(clientLimitLabel(base)).toBe('Até 5 clientes');
    expect(clientLimitLabel({ ...base, clientLimit: null })).toBe('Clientes ilimitados');
  });
});

describe('freePlanNote', () => {
  it('deriva a nota do plano gratuito', () => {
    expect(freePlanNote([base])).toBe('Plano Free até 5 clientes, sem cartão.');
  });

  it('fica vazia se não houver plano gratuito com limite', () => {
    expect(freePlanNote([{ ...base, monthlyPriceEur: 20 }])).toBe('');
  });
});
