/**
 * ClientLayout.jsx — layout do portal do cliente.
 *
 * Navegação simplificada: apenas as secções que o cliente pode ver.
 * Sem acesso a funcionalidades de gestão — tudo é read-only excepto check-ins.
 * O branding (logo, cor) do Personal Trainer é aplicado via AuthContext no login.
 */

import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, Outlet } from 'react-router-dom';
import {
  LayoutDashboard,
  Dumbbell,
  UtensilsCrossed,
  ClipboardList,
  UserRound,
  Menu,
  LogOut,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import {
  Sheet,
  SheetContent,
  SheetTrigger,
  SheetTitle,
} from '@/components/ui/sheet';
import { useAuth } from '@/context/useAuth';
import { getPortalBranding } from '@/api/clientPortalApi';

const navItems = [
  { label: 'Dashboard', href: '/cliente/dashboard', icon: LayoutDashboard },
  { label: 'Plano de Treino', href: '/cliente/plano', icon: Dumbbell },
  { label: 'Nutrição', href: '/cliente/nutricao', icon: UtensilsCrossed },
  { label: 'Check-Ins', href: '/cliente/checkins', icon: ClipboardList },
  { label: 'Perfil', href: '/cliente/perfil', icon: UserRound },
];

function getInitials(name = '') {
  return name
    .split(' ')
    .slice(0, 2)
    .map((n) => n[0])
    .join('')
    .toUpperCase();
}

function NavContent({ onNavigate }) {
  const location = useLocation();
  const { user, trainerSettings, logout, fetchClientBranding } = useAuth();
  const [brandingFallback, setBrandingFallback] = useState(null);

  useEffect(() => {
    if (trainerSettings?.logo_url || trainerSettings?.app_name) return;

    let active = true;

    getPortalBranding()
      .then((data) => {
        if (!active) return;
        setBrandingFallback(data);
      })
      .catch(() => {
        // Mantemos o fallback visual local caso o branding nao esteja disponivel.
      });

    fetchClientBranding?.().catch(() => {
      // O contexto tenta sincronizar o branding global em paralelo.
    });

    return () => {
      active = false;
    };
  }, [trainerSettings?.app_name, trainerSettings?.logo_url, fetchClientBranding]);

  const resolvedBranding = useMemo(
    () => ({
      app_name: trainerSettings?.app_name ?? brandingFallback?.app_name ?? 'PT Manager',
      logo_url: trainerSettings?.logo_url ?? brandingFallback?.logo_url ?? null,
    }),
    [trainerSettings?.app_name, trainerSettings?.logo_url, brandingFallback]
  );

  const logoUrl = resolvedBranding.logo_url;
  const AppName = resolvedBranding.app_name;

  return (
    <div className="flex h-full flex-col">
      {/* Logo do Personal Trainer ou fallback */}
      <div className="flex flex-col items-center justify-center px-4 py-5 border-b border-border gap-2">
        {logoUrl ? (
          <>
            <img
              src={logoUrl}
              alt={AppName}
              className="max-h-full max-w-full rounded-lg object-contain"
            />
            <span className="font-bold text-base text-foreground text-center">
              {AppName}
            </span>
          </>
        ) : (
          <>
            <div className="h-12 w-12 rounded-xl bg-primary/10 flex items-center justify-center shrink-0">
              <Dumbbell className="h-7 w-7 text-primary" />
            </div>
            <span className="font-bold text-base text-foreground text-center">
              {AppName}
            </span>
          </>
        )}
      </div>

      {/* Navegação */}
      <nav className="flex-1 px-3 py-4">
        <ul className="flex flex-col gap-1">
          {navItems.map((item) => {
            const isActive = location.pathname.startsWith(item.href);
            return (
              <li key={item.href}>
                <Link
                  to={item.href}
                  onClick={onNavigate}
                  className={cn(
                    'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                    isActive
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:bg-accent hover:text-foreground'
                  )}
                >
                  <item.icon className="h-5 w-5 shrink-0" />
                  <span>{item.label}</span>
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      {/* Rodapé */}
      <div className="border-t border-border px-3 py-4">
        <div className="flex items-center gap-3">
          <Avatar className="h-8 w-8 shrink-0">
            <AvatarFallback className="bg-primary/10 text-primary text-xs font-semibold">
              {getInitials(user?.full_name)}
            </AvatarFallback>
          </Avatar>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-foreground truncate">
              {user?.full_name}
            </p>
            <p className="text-xs text-muted-foreground">Cliente</p>
          </div>
          <Button
            variant="ghost"
            size="icon"
            onClick={logout}
            className="shrink-0 text-muted-foreground hover:text-destructive"
            title="Terminar sessão"
          >
            <LogOut className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}

export default function ClientLayout() {
  const [open, setOpen] = useState(false);
  const { trainerSettings, fetchClientBranding } = useAuth();
  const [brandingFallback, setBrandingFallback] = useState(null);

  useEffect(() => {
    if (trainerSettings?.logo_url || trainerSettings?.app_name) return;

    let active = true;

    getPortalBranding()
      .then((data) => {
        if (!active) return;
        setBrandingFallback(data);
      })
      .catch(() => {
        // Sem branding remoto, mantemos o fallback standard do portal.
      });

    fetchClientBranding?.().catch(() => {
      // O contexto tenta sincronizar o branding global em paralelo.
    });

    return () => {
      active = false;
    };
  }, [trainerSettings?.app_name, trainerSettings?.logo_url, fetchClientBranding]);

  const resolvedBranding = useMemo(
    () => ({
      app_name: trainerSettings?.app_name ?? brandingFallback?.app_name ?? 'PT Manager',
      logo_url: trainerSettings?.logo_url ?? brandingFallback?.logo_url ?? null,
    }),
    [trainerSettings?.app_name, trainerSettings?.logo_url, brandingFallback]
  );

  const logoUrl = resolvedBranding.logo_url;
  const AppName = resolvedBranding.app_name;

  return (
    <div className="min-h-screen bg-background text-foreground">
      {/* Sidebar desktop */}
      <aside className="hidden lg:flex lg:w-64 lg:flex-col lg:fixed lg:inset-y-0 lg:z-50 border-r border-border bg-sidebar">
        <NavContent />
      </aside>

      {/* Header mobile */}
      <header className="sticky top-0 z-40 flex items-center justify-between bg-background border-b border-border px-4 py-3 lg:hidden">
        <Sheet open={open} onOpenChange={setOpen}>
          <SheetTrigger asChild>
            <Button variant="ghost" size="icon">
              <Menu className="h-5 w-5" />
            </Button>
          </SheetTrigger>
          <SheetContent
            side="left"
            className="w-64 p-0 bg-sidebar border-border"
          >
            <SheetTitle className="sr-only">Menu</SheetTitle>
            <NavContent onNavigate={() => setOpen(false)} />
          </SheetContent>
        </Sheet>
        {logoUrl ? (
          <div className="flex items-center gap-2 min-w-0">
            <img
              src={logoUrl}
              alt={AppName}
              className="h-8 w-auto object-contain shrink-0"
            />
            <span className="font-semibold text-foreground truncate">
              {AppName}
            </span>
          </div>
        ) : (
          <div className="flex items-center gap-2">
            <Dumbbell className="h-5 w-5 text-primary" />
            <span className="font-semibold text-foreground">{AppName}</span>
          </div>
        )}
        <div className="w-9" />
      </header>

      <main className="lg:pl-64 min-h-screen">
        <Outlet />
      </main>
    </div>
  );
}
