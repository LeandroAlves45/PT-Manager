import { siteConfig } from '@/config/env';
import { buildSiteDocuments, findSiteDocument, legalReviewed } from '@/content/legal';
import { contactEmail } from '@/content/navigation';
import type { PagePath, SiteDocument, TextLink } from '@/domain/types';

/** Páginas de texto com os URLs reais da app e o email de contacto. */
export const siteDocuments: readonly SiteDocument[] = buildSiteDocuments({
  appUrl: siteConfig.appUrl,
  contactEmail,
});

/** Todas as rotas a pré-renderizar, a inicial primeiro. */
export const pagePaths: readonly PagePath[] = ['/', ...siteDocuments.map((doc) => doc.path)];

/** Ligações do rodapé, pela ordem dos documentos. */
export const footerLinks: readonly TextLink[] = siteDocuments.map((doc) => ({
  label: doc.linkLabel,
  href: doc.path,
}));

/** Aviso nas páginas de texto enquanto não houver revisão jurídica. */
export const draftNotice = legalReviewed
  ? undefined
  : 'Versão provisória: este texto está em revisão e pode mudar antes do lançamento.';

/**
 * Normaliza o caminho pedido (`/privacidade/` ou `/privacidade.html` → `/privacidade`).
 * Um caminho desconhecido devolve `undefined`.
 */
export function resolvePagePath(raw: string): PagePath | undefined {
  const path = raw.replace(/\.html$/, '').replace(/\/+$/, '') || '/';
  if (path === '/' || path === '/index') return '/';
  return findSiteDocument(siteDocuments, path)?.path;
}
