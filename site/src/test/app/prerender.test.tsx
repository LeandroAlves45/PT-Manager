import { describe, expect, it } from 'vitest';

import { render } from '@/app/entry-server';

/**
 * O HTML pré-renderizado é o que o crawler e o browser recebem antes do JavaScript.
 * Estas verificações são as que a CSP de produção e o SEO dependem.
 */
describe('HTML pré-renderizado', () => {
  const { html, head, siteUrl } = render();

  it('contém a página completa (todas as secções e o h1) sem precisar de JS', () => {
    for (const id of ['topo', 'conteudo', 'funcionalidades', 'planos', 'faq', 'contacto']) {
      expect(html).toContain(`id="${id}"`);
    }
    expect(html.match(/<h1\b/g)).toHaveLength(1);
  });

  it('não tem atributos style nem scripts inline (CSP style-src/script-src self)', () => {
    expect(html).not.toMatch(/\sstyle="/);
    expect(html).not.toMatch(/<script\b/);
    expect(html).not.toMatch(/\son[a-z]+="/);
  });

  it('o único script do head é JSON-LD (não executável)', () => {
    const scripts = head.match(/<script[^>]*>/g) ?? [];
    expect(scripts).toEqual(['<script type="application/ld+json">']);
  });

  it('CTAs apontam para a app em https e nenhum link é javascript:', () => {
    expect(html).toContain('href="https://app.ptmanager.pt/auth/login"');
    expect(html).not.toMatch(/href="javascript:/i);
    expect(html).not.toContain('target="_blank"');
    expect(siteUrl).toBe('https://ptmanager.pt');
  });

  it('não carrega recursos de terceiros', () => {
    expect(html + head).not.toMatch(/unpkg|googleapis|gstatic|cdn\./);
  });
});
