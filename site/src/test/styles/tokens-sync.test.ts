import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { describe, expect, it } from 'vitest';

/** Extrai as custom properties do primeiro bloco `.dark { … }` de um CSS. */
function darkTokens(path: string): Record<string, string> {
  const css = readFileSync(path, 'utf8');
  const block = /\.dark\s*\{([^}]*)\}/.exec(css)?.[1];
  if (!block) throw new Error(`Bloco .dark não encontrado em ${path}`);
  return Object.fromEntries(
    [...block.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)].map((m) => [m[1]!, m[2]!.trim()])
  );
}

describe('tokens de marca', () => {
  it('o tema escuro do site é igual ao do frontend', () => {
    const site = darkTokens(resolve(__dirname, '../../styles/globals.css'));
    const app = darkTokens(
      resolve(__dirname, '../../../../frontend/src/shared/styles/globals.css')
    );
    expect(Object.keys(site).length).toBeGreaterThan(20);
    expect(site).toEqual(app);
  });
});
