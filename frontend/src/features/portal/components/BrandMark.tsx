import type { components } from "@/shared/api/schema";
import { brandInitial, DEFAULT_APP_NAME } from "@/shared/lib/brandTheme";

type PortalBranding = components['schemas']['PortalBrandingResponse'];

/**
 * Marca do personal trainer no cabeçalho do portal: logo de 32 px e nome da app.
 *
 * Sem logo há duas saídas:
 * - sem marca própria (nome por omissão) → logo PT Manager;
 * - com nome próprio → monograma com a inicial, numa caixa de raio 8 px.
 *
 * O logo é decorativo (`alt=""`): o nome da app está ao lado e é ele que se lê.
 */
export function BrandMark({
  branding,
}: {
  branding: Pick<PortalBranding, 'app_name' | 'logo_url'>;
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
      <span className="font-display truncate text-lg">{appName}</span>
    </span>
  );
}
