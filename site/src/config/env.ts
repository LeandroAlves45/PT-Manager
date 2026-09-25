/**
 * Configuração pública do site, lida das variáveis `VITE_*` no build.
 *
 * Não há segredos aqui: tudo o que o Vite expõe acaba no bundle. O objetivo é outro:
 * garantir que uma origem mal escrita (http, com path, com credenciais) nunca chega a um
 * `href` ou ao canonical. Um valor inválido rebenta o build em vez de publicar um link
 * errado ou inseguro.
 */

export interface SiteConfig {
  /** Origem pública do site, sem barra final (ex. `https://ptmanager.pt`). */
  siteUrl: string;
  /** Origem da aplicação, sem barra final (ex. `https://app.ptmanager.pt`). */
  appUrl: string;
}

/** Valores de produção, usados quando a variável não está definida. */
export const DEFAULT_SITE_URL = 'https://ptmanager.pt';
export const DEFAULT_APP_URL = 'https://app.ptmanager.pt';

/**
 * Valida e normaliza uma origem.
 *
 * Aceita apenas `https:` (ou `http:` em localhost, para desenvolvimento), sem utilizador,
 * password, path, query nem fragmento. Devolve a origem canónica.
 *
 * @throws Error com o nome da variável quando o valor não é uma origem segura.
 */
export function parseOrigin(name: string, raw: string | undefined, fallback: string): string {
  const value = raw?.trim() || fallback;
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new Error(`${name} não é um URL válido: "${value}"`);
  }

  const isLocalhost = url.hostname === 'localhost' || url.hostname === '127.0.0.1';
  const protocolOk = url.protocol === 'https:' || (url.protocol === 'http:' && isLocalhost);
  if (!protocolOk) throw new Error(`${name} tem de usar https: "${value}"`);
  if (url.username || url.password) throw new Error(`${name} não pode ter credenciais`);
  if (url.pathname !== '/' || url.search || url.hash) {
    throw new Error(`${name} tem de ser só a origem, sem path/query/fragmento: "${value}"`);
  }
  return url.origin;
}

/** Lê a configuração a partir de um objeto de ambiente (injetável para testes). */
export function readSiteConfig(env: Record<string, string | undefined>): SiteConfig {
  return {
    siteUrl: parseOrigin('VITE_SITE_URL', env.VITE_SITE_URL, DEFAULT_SITE_URL),
    appUrl: parseOrigin('VITE_APP_URL', env.VITE_APP_URL, DEFAULT_APP_URL),
  };
}

export const siteConfig: SiteConfig = readSiteConfig(import.meta.env);
