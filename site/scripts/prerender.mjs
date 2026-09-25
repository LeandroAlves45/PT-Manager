/**
 * Pré-render do site (último passo de `npm run build`).
 *
 * 1. Importa `render()` do bundle SSR (`dist-ssr/entry-server.js`).
 * 2. Injeta o HTML da página e o `<head>` (SEO) no `dist/index.html` do build do cliente.
 * 3. Gera `robots.txt` e `sitemap.xml` a partir da origem validada em `config/env.ts`.
 * 4. Apaga `dist-ssr/`, que nunca é publicado.
 *
 * Falha (exit 1) se um marcador faltar: publicar uma página vazia seria pior do que
 * não publicar.
 */
import { readFile, rm, writeFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const distDir = `${root}dist`;
const ssrEntry = pathToFileURL(`${root}dist-ssr/entry-server.js`).href;

/** @type {{ render: () => { html: string; head: string; siteUrl: string } }} */
const { render } = await import(ssrEntry);
const { html, head, siteUrl } = render();

const templatePath = `${distDir}/index.html`;
const template = await readFile(templatePath, 'utf8');
for (const marker of ['<!--app-head-->', '<!--app-html-->']) {
  if (!template.includes(marker)) throw new Error(`Marcador ${marker} em falta no index.html`);
}

// Funções de substituição: um `$` no conteúdo nunca é interpretado como padrão.
const page = template.replace('<!--app-head-->', () => head).replace('<!--app-html-->', () => html);
await writeFile(templatePath, page);

const today = new Date().toISOString().slice(0, 10);
await writeFile(
  `${distDir}/sitemap.xml`,
  `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>${siteUrl}/</loc>
    <lastmod>${today}</lastmod>
  </url>
</urlset>
`
);
await writeFile(
  `${distDir}/robots.txt`,
  `User-agent: *\nAllow: /\n\nSitemap: ${siteUrl}/sitemap.xml\n`
);

await rm(`${root}dist-ssr`, { recursive: true, force: true });
console.log(
  `prerender: dist/index.html (${page.length} bytes), sitemap.xml, robots.txt → ${siteUrl}`
);
