/**
 * Pré-render do site (último passo de `npm run build`).
 *
 * 1. Importa `render()` e `pagePaths` do bundle SSR (`dist-ssr/entry-server.js`).
 * 2. Para cada rota, injeta o HTML e o `<head>` (SEO) numa cópia do `dist/index.html` do
 *    build do cliente: `/` → `index.html`, `/privacidade` → `privacidade.html`. Com
 *    `cleanUrls` a Vercel serve `privacidade.html` em `/privacidade`.
 * 3. Gera `sitemap.xml` (só as páginas indexáveis) e `robots.txt`.
 * 4. Apaga `dist-ssr/`, que nunca é publicado.
 *
 * O `lastmod` vem da data de revisão de cada texto, não da hora do build: um sitemap que
 * muda a cada deploy sem o conteúdo mudar ensina o Google a ignorar o campo.
 *
 * Falha (exit 1) se um marcador faltar: publicar uma página vazia seria pior do que
 * não publicar.
 */
import { readFile, rm, writeFile } from 'node:fs/promises';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = fileURLToPath(new URL('..', import.meta.url));
const distDir = `${root}dist`;
const ssrEntry = pathToFileURL(`${root}dist-ssr/entry-server.js`).href;

/**
 * @typedef {{ path: string; html: string; head: string; siteUrl: string; indexable: boolean; lastModified?: string }} PrerenderResult
 * @type {{ render: (path: string) => PrerenderResult; pagePaths: readonly string[] }}
 */
const { render, pagePaths } = await import(ssrEntry);

const template = await readFile(`${distDir}/index.html`, 'utf8');
for (const marker of ['<!--app-head-->', '<!--app-html-->']) {
  if (!template.includes(marker)) throw new Error(`Marcador ${marker} em falta no index.html`);
}

/** @param {string} path */
const fileFor = (path) => (path === '/' ? 'index.html' : `${path.slice(1)}.html`);

/** @type {PrerenderResult[]} */
const pages = pagePaths.map((path) => render(path));
const siteUrl = pages[0].siteUrl;

for (const { path, head, html } of pages) {
  // Funções de substituição: um `$` no conteúdo nunca é interpretado como padrão.
  const page = template
    .replace('<!--app-head-->', () => head)
    .replace('<!--app-html-->', () => html);
  await writeFile(`${distDir}/${fileFor(path)}`, page);
}

const urls = pages
  .filter((page) => page.indexable)
  .map((page) => {
    const loc = page.path === '/' ? `${siteUrl}/` : `${siteUrl}${page.path}`;
    const lastmod = page.lastModified ? `\n    <lastmod>${page.lastModified}</lastmod>` : '';
    return `  <url>\n    <loc>${loc}</loc>${lastmod}\n  </url>`;
  });

await writeFile(
  `${distDir}/sitemap.xml`,
  `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls.join('\n')}
</urlset>
`
);
await writeFile(
  `${distDir}/robots.txt`,
  `User-agent: *\nAllow: /\n\nSitemap: ${siteUrl}/sitemap.xml\n`
);

await rm(`${root}dist-ssr`, { recursive: true, force: true });
console.log(
  `prerender: ${pages.map((page) => fileFor(page.path)).join(', ')}, sitemap.xml (${urls.length} URL), robots.txt → ${siteUrl}`
);
