import { ChevronsLeft, ChevronsRight, Menu, Search } from 'lucide-react';
import { useState } from 'react';
import { Outlet } from 'react-router';

import { ProfileMenu } from '@/app/layouts/components/ProfileMenu';
import { SidebarNav } from '@/app/layouts/components/SidebarNav';
import { SubscriptionCard } from '@/app/layouts/components/SubscriptionCard';
import { useAuth } from '@/app/providers/useAuth';
import { Breadcrumbs } from '@/shared/components/Breadcrumbs';
import { CommandMenu } from '@/app/layouts/components/CommandMenu';
import { ThemeToggle } from '@/app/components/ThemeToggle';
import { Button } from '@/shared/components/ui/button';
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from '@/shared/components/ui/sheet';
import { navigationFor } from '@/shared/config/navigation';
import { useMediaQuery } from '@/shared/hooks/useMediaQuery';
import { cn } from '@/shared/lib/utils';

/**
 * Moldura da aplicação para o trainer e o superuser.
 *
 * Medidas do desenho aprovado: sidebar de 248 px
 * expandida e 72 px recolhida, topbar de 56 px com efeito de vidro, conteúdo com 24 px de
 * padding e 1200 px de largura máxima. Abaixo de 768 px a sidebar passa a `Sheet` de
 * 312 px.
 *
 * O portal do cliente não usa esta moldura: é mobile-first com barra inferior
 * (ver `PortalLayout`).
 */
export function AppShell() {
  const { session } = useAuth();
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const isDesktop = useMediaQuery('(min-width: 768px)');

  if (session === null) return null;

  const groups = navigationFor(session.role);

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
          <div className="flex h-14 items-center justify-between px-3">
            <span className="font-display text-sidebar-foreground text-lg">
              {collapsed ? 'PT' : 'PT Manager'}
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
        <header className="border-border bg-background/72 sticky top-0 z-10 flex h-14 items-center gap-3 border-b px-4 backdrop-blur-[14px]">
          {!isDesktop && (
            <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
              <SheetTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Abrir navegação">
                  <Menu aria-hidden />
                </Button>
              </SheetTrigger>
              <SheetContent side="left" className="w-78 p-3">
                <SheetTitle className="font-display px-1 text-lg">PT Manager</SheetTitle>
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

          <Breadcrumbs className="flex-1" />

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
