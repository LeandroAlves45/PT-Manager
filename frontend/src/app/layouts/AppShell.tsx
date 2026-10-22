import { ChevronsLeft, ChevronsRight, Menu, Search } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Outlet } from 'react-router';

import { ProfileMenu } from '@/app/layouts/components/ProfileMenu';
import { SidebarNav } from '@/app/layouts/components/SidebarNav';
import { SubscriptionCard } from '@/app/layouts/components/SubscriptionCard';
import { useAuth } from '@/app/providers/useAuth';
import { useTheme } from '@/app/providers/useTheme';
import { Breadcrumbs } from '@/shared/components/Breadcrumbs';
import { BrandMark } from '@/shared/components/BrandMark';
import { CommandMenu } from '@/app/layouts/components/CommandMenu';
import { ThemeToggle } from '@/app/components/ThemeToggle';
import { useTrainerSettingsQuery } from '@/features/trainer-settings/api/settings';
import { Button } from '@/shared/components/ui/button';
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/shared/components/ui/sheet';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { navigationFor } from '@/shared/config/navigation';
import { useBrandVariables } from '@/shared/hooks/useBrandVariables';
import { useDocumentTitle } from '@/shared/hooks/useDocumentTitle';
import { useMediaQuery } from '@/shared/hooks/useMediaQuery';
import { DEFAULT_APP_NAME, resolveBrand, type BrandPair } from '@/shared/lib/brandTheme';
import { cn } from '@/shared/lib/utils';

/** Marca já resolvida que a moldura desenha. `null` mantém a marca PT Manager. */
interface ShellBrand {
  /** Logo e nome da app, ou `null` enquanto carrega. */
  readonly mark: { app_name: string; logo_url: string | null } | null;
  /** Cor de fundo da barra de topo com texto AA, ou `null` para o vidro do tema. */
  readonly header: BrandPair | null;
}

const PT_MANAGER_BRAND: ShellBrand = {
  mark: { app_name: DEFAULT_APP_NAME, logo_url: null },
  header: null,
};

/**
 * Moldura da aplicação para o personal trainer e o superuser.
 *
 * Medidas do desenho aprovado: sidebar de 248 px
 * expandida e 72 px recolhida, topbar de 56 px com efeito de vidro, conteúdo com 24 px de
 * padding e 1200 px de largura máxima. Abaixo de 768 px a sidebar passa a `Sheet` de
 * 312 px.
 *
 * O personal trainer vê a própria marca: nome e logo na sidebar e no menu, a cor
 * principal nos botões e destaques, e a cor de fundo na barra de topo. O superuser fica
 * sempre com a marca PT Manager.
 *
 * O portal do cliente não usa esta moldura: é mobile-first com barra inferior
 * (ver `PortalLayout`).
 */
export function AppShell() {
  const { session } = useAuth();

  if (session === null) return null;

  return session.role === 'trainer' ? <TrainerShell /> : <ShellFrame brand={PT_MANAGER_BRAND} />;
}

/**
 * Lê a marca do personal trainer (a mesma cache que «Marca própria» atualiza ao guardar, por isso muda
 * logo, sem recarregar) e aplica-a ao documento enquanto a área do personal trainer está montada.
 * Se a marca não carregar, a área continua com a marca PT Manager: é decoração.
 */
function TrainerShell() {
  const { resolved } = useTheme();
  const settings = useTrainerSettingsQuery();
  const data = settings.data;

  const resolvedBrand = data === undefined ? null : resolveBrand(data, resolved);
  useBrandVariables(resolvedBrand?.primary ?? null);
  useDocumentTitle(data?.app_name);

  const brand: ShellBrand = settings.isError
    ? PT_MANAGER_BRAND
    : {
        mark: data === undefined ? null : { app_name: data.app_name, logo_url: data.logo_url },
        header: resolvedBrand?.header ?? null,
      };

  return <ShellFrame brand={brand} />;
}

function ShellFrame({ brand }: { brand: ShellBrand }) {
  const { session } = useAuth();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const isDesktop = useMediaQuery('(min-width: 768px)');

  if (session === null) return null;

  const groups = navigationFor(session.role);
  const header = brand.header;

  return (
    <div className="bg-background flex min-h-dvh">
      <CommandMenu />

      {isDesktop && (
        <aside
          className={cn(
            'border-sidebar-border bg-sidebar sticky top-0 flex h-dvh flex-col border-r transition-[width] duration-200',
            collapsed ? 'w-18' : 'w-62'
          )}
        >
          <div className="flex h-14 items-center justify-between gap-2 px-3">
            <span className="text-sidebar-foreground min-w-0" data-testid="shell-brand">
              <ShellBrandMark brand={brand} showName={!collapsed} wrapName />
            </span>
            <Button
              variant="ghost"
              size="icon"
              aria-label={collapsed ? 'Expandir navegação' : 'Recolher navegação'}
              onClick={() => setCollapsed((value) => !value)}
            >
              {collapsed ? <ChevronsRight aria-hidden /> : <ChevronsLeft aria-hidden />}
            </Button>
          </div>

          <div className="flex-1 overflow-y-auto px-2 py-2">
            <SidebarNav groups={groups} collapsed={collapsed} />
          </div>

          {session.role === 'trainer' && !collapsed && (
            <div className="p-3">
              <SubscriptionCard />
            </div>
          )}
        </aside>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header
          data-testid="shell-topbar"
          className={cn(
            'border-border sticky top-0 z-10 flex h-14 items-center gap-3 border-b px-4',
            header === null && 'bg-background/72 backdrop-blur-[14px]'
          )}
          style={
            header === null
              ? undefined
              : { backgroundColor: header.background, color: header.foreground }
          }
        >
          {!isDesktop && (
            <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
              <SheetTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Abrir navegação">
                  <Menu aria-hidden />
                </Button>
              </SheetTrigger>
              <SheetContent side="left" className="w-78 p-3">
                <SheetTitle className="px-1">
                  <ShellBrandMark brand={brand} showName />
                </SheetTitle>
                <div className="pt-4">
                  <SidebarNav
                    groups={groups}
                    collapsed={false}
                    onNavigate={() => setMobileOpen(false)}
                  />
                </div>
                {session.role === 'trainer' && (
                  <div className="pt-4">
                    <SubscriptionCard />
                  </div>
                )}
              </SheetContent>
            </Sheet>
          )}

          <Breadcrumbs className="flex-1" inheritColor={header !== null} />

          <div className="flex items-center gap-1">
            <Button
              variant="outline"
              size="sm"
              className="text-muted-foreground hidden gap-2 sm:flex"
              onClick={() =>
                document.dispatchEvent(
                  new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true })
                )
              }
            >
              <Search aria-hidden className="size-3.5" />
              Pesquisar…
              <kbd className="tabular text-[10px] opacity-70">Ctrl K</kbd>
            </Button>
            <ThemeToggle />
            <ProfileMenu />
          </div>
        </header>

        <main className="mx-auto flex w-full max-w-300 flex-1 flex-col gap-4.5 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

/** Logo e nome da marca; esqueleto enquanto a marca do personal trainer carrega. */
function ShellBrandMark({
  brand,
  showName,
  wrapName = false,
}: {
  brand: ShellBrand;
  showName: boolean;
  wrapName?: boolean;
}): ReactNode {
  if (brand.mark === null) {
    return (
      <Skeleton
        role="status"
        className={cn('h-8', showName ? 'w-36' : 'w-8')}
        aria-label="A carregar a marca…"
      />
    );
  }

  return <BrandMark branding={brand.mark} showName={showName} wrapName={wrapName} />;
}
