import type { components } from '@/shared/api/schema';
import { brandInitial, DEFAULT_APP_NAME } from '@/shared/lib/brandTheme';
import { cn } from '@/shared/lib/utils';

type PortalBranding = components['schemas']['PortalBrandingResponse'];

/**
 * Marca do personal trainer: logo de 32 px e nome da app. Usada no cabeçalho do portal e na
 * shell do personal trainer; com `showName={false}` mostra só o logo (sidebar recolhida).
 *
 * Sem logo há duas saídas:
 * - sem marca própria (nome por omissão) → logo PT Manager;
 * - com nome próprio → monograma com a inicial, numa caixa de raio 8 px.
 *
 * O logo é decorativo (`alt=""`): o nome da app está ao lado e é ele que se lê.
 */
export function BrandMark({
  branding,
  showName = true,
  wrapName = false,
}: {
  branding: Pick<PortalBranding, 'app_name' | 'logo_url'>;
  showName?: boolean;
  /**
   * `true` em espaços estreitos (sidebar de 248 px): o nome ocupa até duas linhas num tamanho
   * menor, em vez de reticências. Os nomes chegam a 50 caracteres.
   */
  wrapName?: boolean;
}) {
  const appName = branding.app_name.trim() === '' ? DEFAULT_APP_NAME : branding.app_name.trim();

  return (
    <span className="flex min-w-0 items-center gap-2">
      {branding.logo_url !== null ? (
        <img
          src={branding.logo_url}
          alt=""
          data-testid="brand-logo"
          className="size-8 shrink-0 rounded-lg object-contain"
        />
      ) : appName === DEFAULT_APP_NAME ? (
        <img src="/logo.svg" alt="" data-testid="brand-default-logo" className="size-8 shrink-0" />
      ) : (
        <span
          aria-hidden
          data-testid="brand-monogram"
          className="bg-primary text-primary-foreground font-display flex size-8 shrink-0 items-center justify-center rounded-lg text-lg leading-none"
        >
          {brandInitial(appName)}
        </span>
      )}
      {showName ? (
        // `pe-1`: a font-display é itálica e o `truncate` cortava a última letra.
        <span
          className={cn(
            'font-display pe-1',
            wrapName ? 'line-clamp-2 text-base leading-tight break-words' : 'truncate text-lg'
          )}
        >
          {appName}
        </span>
      ) : (
        <span className="sr-only">{appName}</span>
      )}
    </span>
  );
}
