import { siteConfig, type SiteConfig } from '@/config/env';

export interface AppLinks {
  login: string;
  signup: string;
}

/**
 * Destinos dos CTAs na aplicação.
 *
 * Ainda não existe registo público na app: "Criar conta" leva também ao login. Quando o
 * signup existir, muda só a linha de `signup`.
 */
export function buildAppLinks(config: SiteConfig): AppLinks {
  return {
    login: `${config.appUrl}/auth/login`,
    signup: `${config.appUrl}/auth/login`,
  };
}

export const appLinks: AppLinks = buildAppLinks(siteConfig);
