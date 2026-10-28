import { siteConfig, type SiteConfig } from '@/config/env';

export interface AppLinks {
  login: string;
  signup: string;
}

/**
 * Destinos dos CTAs na aplicação.
 *
 * Os caminhos são os de `frontend/src/shared/config/auth-routes.ts` (`/entrar` e
 * `/criar-conta`). O antigo `/auth/login` ainda redireciona, mas custava um salto a mais.
 */
export function buildAppLinks(config: SiteConfig): AppLinks {
  return {
    login: `${config.appUrl}/entrar`,
    signup: `${config.appUrl}/criar-conta`,
  };
}

export const appLinks: AppLinks = buildAppLinks(siteConfig);
