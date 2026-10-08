import { useEffect } from 'react';
import { NavLink, Outlet } from 'react-router';

import { ProfileMenu } from '@/app/layouts/components/ProfileMenu';
import { useAuth } from '@/app/providers/useAuth';
import { useTheme } from '@/app/providers/useTheme';
import { ThemeToggle } from '@/app/components/ThemeToggle';
import { BrandMark, usePortalBrandingQuery } from '@/features/portal';
import { navigationFor } from '@/shared/config/navigation';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { DEFAULT_APP_NAME, resolveBrand, type BrandPair } from '@/shared/lib/brandTheme';
import { cn } from '@/shared/lib/utils';

/** Variavéis de `global.css` que a cor principal do personal trainer substitui no portal. */
const BRAND_VARIABLES = ['--primary', '--primary-foreground', '--ring'] as const;

/**
 * Aplica a cor principal do personal trainer ao documento inteiro enquanto o portal está montado.
 *
 * Vai para `document.documentElement` e não para um `div`: diálogos, menus e toasts são
 * renderizados em portais fora da árvore do layout e têm de herdar a mesma cor. O estilo
 * inline ganha a `:root` e a `.dark`; ao sair do portal (logout, troca de papel) as
 * propriedades são removidas e os tokens PT Manager voltam.
 */
function useBrandVariables(primary: BrandPair | null): void {
  const background = primary?.background ?? null;
  const foreground = primary?.foreground ?? null;

  useEffect(() => {
    if (background === null || foreground === null) return;

    const style = document.documentElement.style;
    style.setProperty('--primary', background);
    style.setProperty('--primary-foreground', foreground);
    style.setProperty('--ring', background);

    return () => {
      for (const name of BRAND_VARIABLES) style.removeProperty(name);
    };
  }, [background, foreground]);
}

/**
 * Moldura do portal do cliente: mobile-first, cabeçalho com a marca do personal trainer e barra
 * inferior de cinco itens.
 *
 * Medidas: barra de 64 px mais a safe-area do iOS, alvos de toque de pelo menos 44 px, e o
 * item activo assinalado por ícone, texto e barra superior — nunca só por cor, que é o que
 * exige a WCAG 2.2.
 *
 * Marca: a cor principal é ajustada ao tema e aplicada aos tokens; a cor de fundo só pinta o
 * cabeçalho. Se a marca falhar a carregar, o portal continua com a marca PT Manager: a
 * marca é decoração e não pode bloquear o treino do cliente.
 */
export function PortalLayout() {
  const { session } = useAuth();
  const { resolved } = useTheme();
  const branding = usePortalBrandingQuery();

  const brand = branding.data === undefined ? null : resolveBrand(branding.data, resolved);
  useBrandVariables(brand?.primary ?? null);

  if (session === null) return null;

  const items = navigationFor(session.role).flatMap((group) => group.items);
  const header = brand?.header ?? null;

  return (
    <div className="bg-background flex min-h-dvh flex-col">
      <header
        data-testid="portal-header"
        className={cn(
          'border-border sticky top-0 z-10 flex h-14 items-center justify-between gap-2 border-b px-4',
          header === null && 'bg-background/72 backdrop-blur-[14px]'
        )}
        style={
          header === null
            ? undefined
            : { backgroundColor: header.background, color: header.foreground }
        }
      >
        {branding.isPending ? (
          <Skeleton className="h-8 w-40" aria-label="A carregar a marca…" />
        ) : (
          <BrandMark branding={branding.data ?? { app_name: DEFAULT_APP_NAME, logo_url: null }} />
        )}
        <div className="flex shrink-0 items-center gap-1">
          <ThemeToggle />
          <ProfileMenu />
        </div>
      </header>

      <main className="flex-1 px-4 pt-4 pb-[calc(64px+20px+1rem)]">
        <Outlet />
      </main>

      <nav
        aria-label="Navegação do portal"
        className="border-border bg-card fixed inset-x-0 bottom-0 z-20 border-t pb-[env(safe-area-inset-bottom,20px)]"
      >
        <ul className="flex h-16 items-stretch">
          {items.map((item) => (
            <li key={item.route} className="flex-1">
              <NavLink
                to={item.route}
                // O Início é a rota-pai: sem `end` ficaria ativo em todas as páginas do portal.
                end={item.route === '/portal'}
                className={({ isActive }) =>
                  cn(
                    'relative flex h-full min-h-11 flex-col items-center justify-center gap-1 text-xs',
                    isActive
                      ? 'text-primary before:bg-primary before:absolute before:inset-x-4 before:top-0 before:h-0.5'
                      : 'text-muted-foreground'
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon
                      aria-hidden
                      className={cn('size-5', isActive && 'fill-primary/20')}
                    />
                    <span>{item.label}</span>
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  );
}
