import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

import { brandSwatches } from '@/content/brand';
import { faq } from '@/content/faq';
import { features } from '@/content/features';
import { primaryNav } from '@/content/navigation';
import { plans } from '@/content/plans';
import { swatchClass } from '@/sections/brand-showcase';

/** Lê `SubscriptionTier.cs` do backend: os limites publicados têm de ser os reais. */
function backendTierLimits(): Record<string, number | null> {
  const source = readFileSync(
    resolve(__dirname, '../../../../backend/src/Domain/ValueObjects/SubscriptionTier.cs'),
    'utf8'
  );
  const limits: Record<string, number | null> = {};
  for (const match of source.matchAll(/new\("(\w+)",\s*(\d+|null)\)/g)) {
    limits[match[1]!] = match[2] === 'null' ? null : Number(match[2]);
  }
  return limits;
}

describe('planos', () => {
  it('têm exatamente os tiers e limites de clientes do backend', () => {
    const backend = backendTierLimits();
    expect(Object.keys(backend).sort()).toEqual(['FREE', 'PRO', 'STARTER']);
    expect(Object.fromEntries(plans.map((plan) => [plan.code, plan.clientLimit]))).toEqual(backend);
  });

  it('só o Free é gratuito e há exatamente um plano em destaque', () => {
    expect(plans.filter((plan) => plan.monthlyPriceEur === 0).map((plan) => plan.code)).toEqual([
      'FREE',
    ]);
    expect(plans.filter((plan) => plan.highlighted)).toHaveLength(1);
  });
});

describe('conteúdo', () => {
  it('não tem ids duplicados', () => {
    for (const list of [features, faq, brandSwatches]) {
      const ids = list.map((item) => item.id);
      expect(new Set(ids).size).toBe(ids.length);
    }
  });

  it('não promete cobrança aos clientes do trainer (o Stripe só cobra a subscrição)', () => {
    const text = JSON.stringify([features, faq]).toLowerCase();
    expect(text).not.toMatch(
      /pagamentos automáticos|cobranças recorrentes|stripe connect|conta stripe/
    );
  });

  it('não contém placeholders do design original', () => {
    const text = JSON.stringify([features, faq, plans, primaryNav]);
    expect(text).not.toMatch(/Nome Apelido|@handle|placeholder|por definir|lorem/i);
  });

  it('cada amostra de cor tem uma classe com o mesmo valor', () => {
    for (const swatch of brandSwatches) {
      expect(swatchClass[swatch.id]).toBe(`bg-[${swatch.value}]`);
    }
  });
});
