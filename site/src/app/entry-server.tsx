import { StrictMode } from 'react';
import { renderToString } from 'react-dom/server';

import { App } from '@/app/App';
import { pagePaths, siteDocuments } from '@/app/pages';
import { siteConfig } from '@/config/env';
import { findSiteDocument, legalReviewed } from '@/content/legal';
import { contactEmail } from '@/content/navigation';
import { plans } from '@/content/plans';
import type { PagePath } from '@/domain/types';
import { renderHead } from '@/seo/head';
import { buildJsonLd } from '@/seo/jsonLd';
import { buildDocumentMetadata, buildHomeMetadata } from '@/seo/metadata';

export interface PrerenderResult {
  path: PagePath;
  html: string;
  head: string;
  siteUrl: string;
  /** Entra no sitemap só se for indexável. */
  indexable: boolean;
  /** Data `AAAA-MM-DD` da última revisão do texto (só nas páginas de texto). */
  lastModified?: string;
}

export { pagePaths };

/** Usado apenas por `scripts/prerender.mjs` no build; nunca chega ao browser. */
export function render(path: PagePath = '/'): PrerenderResult {
  const html = renderToString(
    <StrictMode>
      <App path={path} />
    </StrictMode>
  );

  if (path === '/') {
    const meta = buildHomeMetadata(siteConfig);
    return {
      path,
      html,
      head: renderHead(meta, buildJsonLd(siteConfig, plans, contactEmail)),
      siteUrl: siteConfig.siteUrl,
      indexable: meta.indexable,
    };
  }

  const document = findSiteDocument(siteDocuments, path);
  if (!document) throw new Error(`Rota sem página: ${path}`);
  const meta = buildDocumentMetadata(siteConfig, document, legalReviewed);
  return {
    path,
    html,
    head: renderHead(meta),
    siteUrl: siteConfig.siteUrl,
    indexable: meta.indexable,
    lastModified: document.updatedOn,
  };
}
