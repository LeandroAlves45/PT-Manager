import type { SiteConfig } from '@/config/env';
import type { Plan } from '@/domain/types';

/**
 * Dados estruturados (schema.org) da página inicial.
 *
 * Só factos presentes na página: organização, produto e ofertas com o preço mensal. Nada
 * de avaliações ou contagens de utilizadores, que o Google trataria como spam se não
 * estivessem visíveis.
 */
export function buildJsonLd(config: SiteConfig, plans: readonly Plan[], email: string) {
  return {
    '@context': 'https://schema.org',
    '@graph': [
      {
        '@type': 'Organization',
        '@id': `${config.siteUrl}/#organization`,
        name: 'PT Manager',
        url: `${config.siteUrl}/`,
        logo: `${config.siteUrl}/logo.svg`,
        email,
      },
      {
        '@type': 'SoftwareApplication',
        name: 'PT Manager',
        applicationCategory: 'BusinessApplication',
        operatingSystem: 'Web',
        url: `${config.siteUrl}/`,
        inLanguage: 'pt-PT',
        publisher: { '@id': `${config.siteUrl}/#organization` },
        offers: plans.map((plan) => ({
          '@type': 'Offer',
          name: plan.code,
          price: plan.monthlyPriceEur.toFixed(2),
          priceCurrency: 'EUR',
        })),
      },
    ],
  };
}

/** Barra invertida + u003c, construída sem barra literal no código-fonte. */
const ESCAPED_LT = `${String.fromCharCode(92)}u003c`;

/**
 * Serializa para um `<script type="application/ld+json">`.
 *
 * `<` é escapado para `\u003c`: um texto com `</script>` nunca fecha o elemento, que é o
 * vetor clássico de XSS em JSON embutido em HTML.
 */
export function serializeJsonLd(data: unknown): string {
  return JSON.stringify(data).replace(/</g, () => ESCAPED_LT);
}
