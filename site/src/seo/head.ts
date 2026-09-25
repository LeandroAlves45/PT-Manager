import type { PageMetadata } from '@/seo/metadata';
import { serializeJsonLd } from '@/seo/jsonLd';

/** Escapa texto para conteúdo de atributo ou elemento HTML. */
export function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/**
 * Gera o markup do `<head>` (SEO, Open Graph, Twitter e JSON-LD).
 *
 * Função pura sobre dados já validados: o pré-render injeta o resultado no `index.html`,
 * por isso o crawler recebe os metadados sem executar JavaScript. Todos os valores passam
 * por `escapeHtml`; o JSON-LD passa por `serializeJsonLd`.
 */
export function renderHead(meta: PageMetadata, jsonLd: unknown): string {
  const e = escapeHtml;
  const tags = [
    `<title>${e(meta.title)}</title>`,
    `<meta name="description" content="${e(meta.description)}">`,
    `<link rel="canonical" href="${e(meta.canonical)}">`,
    `<meta name="robots" content="index, follow">`,
    `<meta name="theme-color" content="${e(meta.themeColor)}">`,
    `<meta property="og:type" content="website">`,
    `<meta property="og:locale" content="${e(meta.locale)}">`,
    `<meta property="og:site_name" content="${e(meta.siteName)}">`,
    `<meta property="og:title" content="${e(meta.title)}">`,
    `<meta property="og:description" content="${e(meta.description)}">`,
    `<meta property="og:url" content="${e(meta.canonical)}">`,
    `<meta property="og:image" content="${e(meta.image.url)}">`,
    `<meta property="og:image:width" content="${meta.image.width}">`,
    `<meta property="og:image:height" content="${meta.image.height}">`,
    `<meta property="og:image:alt" content="${e(meta.image.alt)}">`,
    `<meta name="twitter:card" content="summary_large_image">`,
    `<meta name="twitter:title" content="${e(meta.title)}">`,
    `<meta name="twitter:description" content="${e(meta.description)}">`,
    `<meta name="twitter:image" content="${e(meta.image.url)}">`,
    `<script type="application/ld+json">${serializeJsonLd(jsonLd)}</script>`,
  ];
  return tags.join('\n    ');
}
