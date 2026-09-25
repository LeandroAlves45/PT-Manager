import { describe, expect, it } from 'vitest';

import { plans } from '@/content/plans';
import { escapeHtml, renderHead } from '@/seo/head';
import { buildJsonLd, serializeJsonLd } from '@/seo/jsonLd';
import { buildHomeMetadata } from '@/seo/metadata';

const config = { siteUrl: 'https://ptmanager.pt', appUrl: 'https://app.ptmanager.pt' };

describe('metadados', () => {
  it('usam URLs absolutos da origem do site e tamanhos adequados a SERP', () => {
    const meta = buildHomeMetadata(config);
    expect(meta.canonical).toBe('https://ptmanager.pt/');
    expect(meta.image.url).toBe('https://ptmanager.pt/og-image.png');
    expect(meta.description.length).toBeGreaterThan(70);
    expect(meta.description.length).toBeLessThanOrEqual(160);
    expect(meta.title.length).toBeLessThanOrEqual(65);
  });
});

describe('JSON-LD', () => {
  it('descreve uma oferta por plano em EUR', () => {
    const data = buildJsonLd(config, plans, 'contacto@ptmanager.pt');
    const app = data['@graph'][1] as { offers: { price: string; priceCurrency: string }[] };
    expect(app.offers).toHaveLength(plans.length);
    expect(app.offers.every((offer) => offer.priceCurrency === 'EUR')).toBe(true);
  });

  it('escapa "<" para não fechar o <script>', () => {
    const payload = { name: '</script><script>alert(1)</script>' };
    const json = serializeJsonLd(payload);
    expect(json).not.toContain('</script>');
    expect(JSON.parse(json)).toEqual(payload);
  });
});

describe('renderHead', () => {
  it('inclui as tags de SEO, Open Graph, Twitter e JSON-LD', () => {
    const head = renderHead(buildHomeMetadata(config), { a: 1 });
    for (const needle of [
      '<title>',
      'name="description"',
      'rel="canonical" href="https://ptmanager.pt/"',
      'property="og:image" content="https://ptmanager.pt/og-image.png"',
      'property="og:locale" content="pt_PT"',
      'name="twitter:card" content="summary_large_image"',
      '<script type="application/ld+json">',
    ]) {
      expect(head).toContain(needle);
    }
  });

  it('escapa valores com aspas e tags', () => {
    const meta = { ...buildHomeMetadata(config), title: '"><script>x</script>' };
    expect(renderHead(meta, {})).not.toContain('<script>x');
    expect(escapeHtml(`<a href="x">'&`)).toBe('&lt;a href=&quot;x&quot;&gt;&#39;&amp;');
  });
});
