import type { SiteConfig } from '@/config/env';

export interface PageMetadata {
  title: string;
  description: string;
  canonical: string;
  locale: string;
  siteName: string;
  image: { url: string; width: number; height: number; alt: string };
  themeColor: string;
}

/** Metadados da página inicial, com URLs absolutos derivados da origem do site. */
export function buildHomeMetadata(config: SiteConfig): PageMetadata {
  return {
    title: 'PT Manager — Gestão para personal trainers e nutricionistas',
    description:
      'Clientes, planos de treino, nutrição, suplementação, sessões e check-ins numa só plataforma, com a tua marca no portal do cliente. Começa grátis até 5 clientes.',
    canonical: `${config.siteUrl}/`,
    locale: 'pt_PT',
    siteName: 'PT Manager',
    image: {
      url: `${config.siteUrl}/og-image.png`,
      width: 1200,
      height: 630,
      alt: 'PT Manager — clientes, treino e nutrição num só lugar',
    },
    themeColor: '#05080c',
  };
}
