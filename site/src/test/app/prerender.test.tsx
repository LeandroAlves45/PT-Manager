import { describe, expect, it } from 'vitest';

import { pagePaths, render } from '@/app/entry-server';
import { resolvePagePath } from '@/app/pages';
import { legalReviewed } from '@/content/legal';

/**
 * O HTML pré-renderizado é o que o crawler e o browser recebem antes do JavaScript.
 * Estas verificações são as que a CSP de produção e o SEO dependem, em todas as rotas.
 */
describe('HTML pré-renderizado: página inicial', () => {
  const { html, head, siteUrl } = render('/');

  it('contém a página completa (todas as secções e o h1) sem precisar de JS', () => {
    for (const id of ['topo', 'conteudo', 'funcionalidades', 'planos', 'faq', 'contacto']) {
      expect(html).toContain(`id="${id}"`);
    }
    expect(html.match(/<h1\b/g)).toHaveLength(1);
  });

  it('o único script do head é JSON-LD (não executável)', () => {
    const scripts = head.match(/<script[^>]*>/g) ?? [];
    expect(scripts).toEqual(['<script type="application/ld+json">']);
  });

  it('CTAs apontam para as rotas reais da app (entrar e criar conta)', () => {
    expect(html).toContain('href="https://app.ptmanager.pt/entrar"');
    expect(html).toContain('href="https://app.ptmanager.pt/criar-conta"');
    expect(html).not.toContain('/auth/login');
    expect(siteUrl).toBe('https://ptmanager.pt');
  });

  it('é indexável e permite pré-visualização grande da imagem', () => {
    expect(head).toContain('content="index, follow, max-image-preview:large"');
  });
});

describe.each(pagePaths)('HTML pré-renderizado: %s', (path) => {
  const { html, head } = render(path);

  it('tem exatamente um h1 e o conteúdo principal', () => {
    expect(html.match(/<h1\b/g)).toHaveLength(1);
    expect(html).toContain('id="conteudo"');
  });

  it('não tem atributos style nem scripts inline (CSP style-src/script-src self)', () => {
    expect(html).not.toMatch(/\sstyle="/);
    expect(html).not.toMatch(/<script\b/);
    expect(html).not.toMatch(/\son[a-z]+="/);
  });

  it('nenhum link é javascript: nem abre noutro separador', () => {
    expect(html).not.toMatch(/href="javascript:/i);
    expect(html).not.toContain('target="_blank"');
  });

  it('não tem ids repetidos (as âncoras do índice têm de ser únicas)', () => {
    const ids = [...html.matchAll(/\sid="([^"]+)"/g)].map((match) => match[1]);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('tem canonical absoluto para a própria rota', () => {
    const expected = path === '/' ? 'https://ptmanager.pt/' : `https://ptmanager.pt${path}`;
    expect(head).toContain(`<link rel="canonical" href="${expected}">`);
  });

  it('o rodapé liga a todas as páginas de texto', () => {
    for (const target of pagePaths.filter((p) => p !== '/')) {
      expect(html).toContain(`href="${target}"`);
    }
  });

  it('não carrega recursos de terceiros', () => {
    expect(html + head).not.toMatch(/unpkg|googleapis|gstatic|cdn\./);
  });

  it('o texto publicado não usa travessões (vírgula, dois pontos ou barra)', () => {
    expect(html + head).not.toMatch(/[–—]/);
  });
});

describe('páginas de texto', () => {
  const documentPaths = pagePaths.filter((path) => path !== '/');

  it('existem as quatro rotas exigidas pelas lojas de apps', () => {
    expect(documentPaths).toEqual(['/privacidade', '/termos', '/suporte', '/eliminar-conta']);
  });

  it.each(documentPaths)('%s não tem scripts no head (nem JSON-LD)', (path) => {
    expect(render(path).head).not.toMatch(/<script\b/);
  });

  it.each(documentPaths)('%s: âncoras do cabeçalho voltam à página inicial', (path) => {
    const { html } = render(path);
    expect(html).toContain('href="/#planos"');
    expect(html).not.toContain('href="#planos"');
  });

  it.each(documentPaths)('%s segue o estado da revisão jurídica (noindex e aviso)', (path) => {
    const { head, html, indexable } = render(path);
    const robots = legalReviewed ? 'index, follow, max-image-preview:large' : 'noindex, follow';
    expect(indexable).toBe(legalReviewed);
    expect(head).toContain(`content="${robots}"`);
    expect(html.includes('Versão provisória')).toBe(!legalReviewed);
  });
});

describe('resolvePagePath', () => {
  it.each([
    ['/', '/'],
    ['/index.html', '/'],
    ['/privacidade', '/privacidade'],
    ['/privacidade/', '/privacidade'],
    ['/eliminar-conta.html', '/eliminar-conta'],
  ])('%s → %s', (raw, expected) => {
    expect(resolvePagePath(raw)).toBe(expected);
  });

  it('devolve undefined para rotas desconhecidas', () => {
    expect(resolvePagePath('/admin')).toBeUndefined();
  });
});
