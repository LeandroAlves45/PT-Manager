import { describe, expect, it } from 'vitest';

import {
  buildSiteDocuments,
  findSiteDocument,
  legalOperator,
  legalReviewed,
  PENDING_FIELD,
  processors,
} from '@/content/legal';

const documents = buildSiteDocuments({
  appUrl: 'https://app.example.pt',
  contactEmail: 'ajuda@example.pt',
});

/** Ids fixos do layout: uma secção de documento com o mesmo id duplicava a âncora. */
const LAYOUT_IDS = ['topo', 'conteudo', 'funcionalidades', 'planos', 'faq', 'contacto'];

describe('documentos legais', () => {
  it('cada documento tem ids de secção únicos e diferentes dos do layout', () => {
    for (const document of documents) {
      const ids = document.sections.map((section) => section.id);
      expect(new Set(ids).size).toBe(ids.length);
      expect(ids.filter((id) => LAYOUT_IDS.includes(id) || id === 'documento-titulo')).toEqual([]);
    }
  });

  it('descriptions com tamanho adequado aos resultados de pesquisa', () => {
    for (const document of documents) {
      expect(document.description.length).toBeGreaterThan(70);
      expect(document.description.length).toBeLessThanOrEqual(160);
    }
  });

  it('usam os URLs e o email recebidos (nada fixo no texto)', () => {
    const text = JSON.stringify(documents);
    expect(text).toContain('https://app.example.pt/entrar');
    expect(text).toContain('https://app.example.pt/recuperar-password');
    expect(text).toContain('ajuda@example.pt');
    expect(text).not.toMatch(/ptmanager\.pt/);
  });

  it('as ligações internas apontam para documentos que existem', () => {
    const internal = documents
      .flatMap((document) => document.sections)
      .flatMap((section) => section.blocks)
      .flatMap((block) => (block.kind === 'links' ? block.items : []))
      .map((link) => link.href)
      .filter((href) => href.startsWith('/'));
    expect(internal.length).toBeGreaterThan(0);
    for (const href of internal) {
      expect(findSiteDocument(documents, href)).toBeDefined();
    }
  });

  it('a página de eliminação explica o que é apagado e o que fica (exigência da Google)', () => {
    const deletion = findSiteDocument(documents, '/eliminar-conta')!;
    const text = JSON.stringify(deletion);
    expect(text).toMatch(/Apagado de imediato/);
    expect(text).toMatch(/Fica com o personal trainer/);
    expect(text).toMatch(/mailto:ajuda@example\.pt/);
  });

  it('a política de privacidade lista os fornecedores e os dados de saúde', () => {
    const text = JSON.stringify(findSiteDocument(documents, '/privacidade'));
    for (const name of ['Render', 'Vercel', 'Neon', 'Resend', 'Stripe', 'Cloudinary', 'Sentry']) {
      expect(processors.some((line) => line.startsWith(name))).toBe(true);
    }
    expect(text).toMatch(/condições médicas/);
    expect(text).toMatch(/CNPD/);
  });

  it('não pode ser marcado como revisto com campos por preencher', () => {
    const pending = Object.values(legalOperator).some((value) => value === PENDING_FIELD);
    expect(legalReviewed && pending).toBe(false);
  });
});
