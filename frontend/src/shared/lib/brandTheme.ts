import type { components } from '@/shared/api/schema';
import { adjustToContrast, isHexColor, pickForeground } from '@/shared/lib/contrast';

type PortalBranding = components['schemas']['PortalBrandingResponse'];

/** Tema efetivo do documento. */
export type BrandSurfaceTheme = 'light' | 'dark';

/** Tokens de um tema que a marca precisa de conhecer fora do CSS. */
export interface BrandSurface {
  /** `--background`: fundo da página. */
  readonly background: string;
  /** `--card`: fundo dos cartões e da barra inferior. */
  readonly card: string;
  /** `--foreground`: texto por omissão. */
  readonly foreground: string;
  /** `--primary` PT Manager, usado quando o trainer não tem cor. */
  readonly primary: string;
  /** `--primary-foreground` PT Manager. */
  readonly primaryForeground: string;
}

/**
 * Valores de `shared/styles/globals.css` por tema.
 *
 * A cor principal aparece como texto sobre `background` e `card`, por isso é contra esses
 * dois que o tom é ajustado; a pré-visualização das definições desenha os dois temas sem
 * depender do tema ativo.
 */
export const BRAND_SURFACES: Readonly<Record<BrandSurfaceTheme, BrandSurface>> = {
  light: {
    background: '#f7f8fa',
    card: '#ffffff',
    foreground: '#0b1220',
    primary: '#0077b6',
    primaryForeground: '#ffffff',
  },
  dark: {
    background: '#05080c',
    card: '#0a0f16',
    foreground: '#f7f8fa',
    primary: '#00a3e9',
    primaryForeground: '#03131c',
  },
};

/** Par de cores já validado: fundo e texto que passa AA sobre ele. */
export interface BrandPair {
  readonly background: string;
  readonly foreground: string;
}

/**
 * Marca do personal trainer resolvida para um tema.
 *
 * Cada parte é `null` quando a cor não existe ou não passa AA: o ecrã fica com o token
 * PT Manager correspondente, nunca com uma cor ilegível.
 */
export interface ResolvedBrand {
  readonly primary: BrandPair | null;
  readonly header: BrandPair | null;
}

/**
 * Resolve a marca do personal trainer para o tema indicado.
 *
 * - `primary_color` é usada como texto sobre o fundo e o cartão, por isso o tom é ajustado
 *   até 4,5:1 contra os dois (mais escuro no claro, mais claro no escuro). O texto em cima
 *   dela é preto ou branco, o que contrastar mais.
 * - `body_color` só pinta o cabeçalho; não é ajustada, porque é o próprio fundo: o texto
 *   passa a preto ou branco.
 */
export function resolveBrand(
  branding: Pick<PortalBranding, 'primary_color' | 'body_color'>,
  theme: BrandSurfaceTheme
): ResolvedBrand {
  return {
    primary: resolvePrimary(branding.primary_color, theme),
    header: resolveHeader(branding.body_color),
  };
}

function resolvePrimary(color: string | null, theme: BrandSurfaceTheme): BrandPair | null {
  if (color === null || !isHexColor(color))
    return null;

  const surface = BRAND_SURFACES[theme];
  const background = adjustToContrast(color, [surface.background, surface.card]);
  if (background === null)
    return null;

  const foreground = pickForeground(background);
  return foreground === null ? null : { background, foreground };
}

function resolveHeader(color: string | null): BrandPair | null {
  if (color === null || !isHexColor(color))
    return null;

  const foreground = pickForeground(color);
  return foreground === null ? null : { background: color.toLowerCase(), foreground };
}

/** Nome por omissão do backend quando o personal trainer nunca configurou a marca. */
export const DEFAULT_APP_NAME = 'PT Manager';

/**
 * Inicial do monograma: a primeira letra ou número do nome da app, em maiúscula.
 * Nomes sem letras (só símbolos) caem na primeira letra visível.
 */
export function brandInitial(appName: string): string {
  const trimmed = appName.trim();
  const match = /[\p{L}\p{N}]/u.exec(trimmed);
  return (match?.[0] ?? trimmed.charAt(0)).toLocaleUpperCase('pt-PT');
}
