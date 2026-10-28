import type { SiteConfig } from '@/config/env';
import type { SiteDocument } from '@/domain/types';

export interface PageMetadata {
  title: string;
  description: string;
  canonical: string;
  locale: string;
  siteName: string;
  image: { url: string; width: number; height: number; alt: string };
  themeColor: string;
  /** `false` gera `noindex` (página acessível por URL, fora dos motores de busca). */
  indexable: boolean;
}

const SITE_NAME = 'PT Manager';

function sharedMetadata(config: SiteConfig) {
  return {
    locale: 'pt_PT',
    siteName: SITE_NAME,
    image: {
      url: `${config.siteUrl}/og-image.png`,
      width: 1200,
      height: 630,
      alt: 'PT Manager: clientes, treino e nutrição num só lugar',
    },
    themeColor: '#05080c',
  };
}

/** Metadados da página inicial, com URLs absolutos derivados da origem do site. */
export function buildHomeMetadata(config: SiteConfig): PageMetadata {
  return {
    ...sharedMetadata(config),
    title: 'PT Manager | Gestão para personal trainers e nutricionistas',
    description:
      'Clientes, planos de treino, nutrição, suplementação, sessões e check-ins numa só plataforma, com a tua marca no portal do cliente. Começa grátis até 5 clientes.',
    canonical: `${config.siteUrl}/`,
    indexable: true,
  };
}

/**
 * Metadados de uma página de texto. O canonical não tem barra final, como os URLs que a
 * Vercel serve com `cleanUrls` e `trailingSlash: false`.
 */
export function buildDocumentMetadata(
  config: SiteConfig,
  document: SiteDocument,
  indexable: boolean
): PageMetadata {
  return {
    ...sharedMetadata(config),
    title: `${document.title} | ${SITE_NAME}`,
    description: document.description,
    canonical: `${config.siteUrl}${document.path}`,
    indexable,
  };
}
