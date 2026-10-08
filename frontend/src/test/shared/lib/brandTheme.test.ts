import { describe, expect, it } from 'vitest';

import {
  BRAND_SURFACES,
  brandInitial,
  resolveBrand,
  type BrandSurfaceTheme,
} from '@/shared/lib/brandTheme';
import { contrastRatio } from '@/shared/lib/contrast';

/**
 * CSS real do disco, relativo à pasta `frontend/` (onde o Vitest corre, localmente e no CI).
 * O tsconfig da app não carrega os tipos do Node, por isso o `fs` vem por import dinâmico
 * (o mesmo padrão do `node:buffer` em `SettingsPage.test.tsx`); o `?raw` do Vite não serve
 * porque o Vitest corre com `css: false` e devolve o CSS vazio.
 */
async function readGlobalsCss(): Promise<string> {
  const { readFileSync } = (await import('node:' + 'fs')) as {
    readFileSync: (path: string, encoding: 'utf8') => string;
  };
  return readFileSync('src/shared/styles/globals.css', 'utf8');
}

/** Lê o valor de um token dentro de um bloco (`:root {` ou `.dark {`) do CSS real. */
function cssToken(css: string, block: string, token: string): string {
  const start = css.indexOf(`${block} {`);
  const body = css.slice(start, css.indexOf('}', start));
  const match = new RegExp(`${token}:\\s*(#[0-9a-fA-F]{6});`).exec(body);
  if (match?.[1] === undefined) throw new Error(`${token} em falta em ${block}`);
  return match[1].toLowerCase();
}

describe('brandTheme', () => {
  it('keeps the surfaces in sync with globals.css', async () => {
    const css = await readGlobalsCss();
    const blocks: Record<BrandSurfaceTheme, string> = { light: ':root', dark: '.dark' };

    for (const theme of ['light', 'dark'] as const) {
      const surface = BRAND_SURFACES[theme];
      expect(surface.background).toBe(cssToken(css, blocks[theme], '--background'));
      expect(surface.card).toBe(cssToken(css, blocks[theme], '--card'));
      expect(surface.foreground).toBe(cssToken(css, blocks[theme], '--foreground'));
      expect(surface.primary).toBe(cssToken(css, blocks[theme], '--primary'));
      expect(surface.primaryForeground).toBe(cssToken(css, blocks[theme], '--primary-foreground'));
    }
  });

  it('adjusts the seed orange per theme and keeps the header colour', () => {
    const branding = { primary_color: '#E8642A', body_color: '#1F1A17' };

    expect(resolveBrand(branding, 'light')).toEqual({
      primary: { background: '#ba5022', foreground: '#ffffff' },
      header: { background: '#1f1a17', foreground: '#ffffff' },
    });
    expect(resolveBrand(branding, 'dark')).toEqual({
      primary: { background: '#e8642a', foreground: '#000000' },
      header: { background: '#1f1a17', foreground: '#ffffff' },
    });
  });

  it('passes AA for text and for the label on the button in both themes', () => {
    for (const theme of ['light', 'dark'] as const) {
      const { primary } = resolveBrand({ primary_color: '#E8642A', body_color: null }, theme);
      if (primary === null) throw new Error('marca em falta');
      const surface = BRAND_SURFACES[theme];

      expect(contrastRatio(primary.background, surface.background)).toBeGreaterThanOrEqual(4.5);
      expect(contrastRatio(primary.background, surface.card)).toBeGreaterThanOrEqual(4.5);
      expect(contrastRatio(primary.background, primary.foreground)).toBeGreaterThanOrEqual(4.5);
    }
  });

  it('uses black text on a light header colour', () => {
    expect(resolveBrand({ primary_color: null, body_color: '#FFE600' }, 'dark').header).toEqual({
      background: '#ffe600',
      foreground: '#000000',
    });
  });

  it('falls back to the PT Manager tokens when there are no colours or they are invalid', () => {
    expect(resolveBrand({ primary_color: null, body_color: null }, 'light')).toEqual({
      primary: null,
      header: null,
    });
    expect(
      resolveBrand({ primary_color: 'url(javascript:1)', body_color: '#12345' }, 'dark')
    ).toEqual({ primary: null, header: null });
  });

  it('builds the monogram from the first letter or digit of the app name', () => {
    expect(brandInitial('Salgado Performance')).toBe('S');
    expect(brandInitial('  ágil coach')).toBe('Á');
    expect(brandInitial('#1 Fitness')).toBe('1');
    expect(brandInitial('***')).toBe('*');
  });
});
