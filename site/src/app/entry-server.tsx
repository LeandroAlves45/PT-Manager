import { StrictMode } from 'react';
import { renderToString } from 'react-dom/server';

import { App } from '@/app/App';
import { siteConfig } from '@/config/env';
import { contactEmail } from '@/content/navigation';
import { plans } from '@/content/plans';
import { renderHead } from '@/seo/head';
import { buildJsonLd } from '@/seo/jsonLd';
import { buildHomeMetadata } from '@/seo/metadata';

export interface PrerenderResult {
  html: string;
  head: string;
  siteUrl: string;
}

/** Usado apenas por `scripts/prerender.mjs` no build; nunca chega ao browser. */
export function render(): PrerenderResult {
  return {
    html: renderToString(
      <StrictMode>
        <App />
      </StrictMode>
    ),
    head: renderHead(buildHomeMetadata(siteConfig), buildJsonLd(siteConfig, plans, contactEmail)),
    siteUrl: siteConfig.siteUrl,
  };
}
