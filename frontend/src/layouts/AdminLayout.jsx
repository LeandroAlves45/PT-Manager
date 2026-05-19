/**
 * AdminLayout.jsx — layout do dashboard do superuser.
 *
 * Completamente separado do TrainerLayout:
 *   - Sidebar própria com navegação de admin
 *   - Sem branding do Personal Trainer (usa sempre o tema PT Manager por defeito)
 *   - Cor primária fixa — não é afectada pelas settings de nenhum Personal Trainer
 */

import { useState } from 'react';
import { Link, useLocation, Outlet } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  Menu,
  LogOut,
  ShieldCheck,
  Dumbbell,
  Apple,
  Pill,
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

const navItems = [
  { label: 'Dashboard', href: '/admin/dashboard', icon: LayoutDashboard },
  { label: 'Trainers', href: '/admin/trainers', icon: Users },
  { label: 'Exercícios', href: '/admin/exercicios', icon: Dumbbell },
  { label: 'Alimentos', href: '/admin/alimentos', icon: Apple },
  { label: 'Suplementos', href: '/admin/suplementos', icon: Pill },
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
  const { user, logout } = useAuth();

  return (
    <div className="flex flex-col h-full">
      {/* Cabeçalho do admin */}
      <div className="flex items-center gap-3 px-4 py-6 border-b border-border">
        <div className="h-8 w-8 rounded-lg bg-primary/10 flex items-center justify-center">
          <ShieldCheck className="h-5 w-5 text-primary" />
        </div>
        <div>
          <p className="font-bold text-sm text-foreground">Pt Manager</p>
          <p className="text-xs text-muted-foreground">
            Painel de administração
          </p>
        </div>
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
                    'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
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

      {/* Rodapé com utilizador e logout */}
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
            <p className="text-xs text-muted-foreground">Superuser</p>
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

export default function AdminLayout() {
  const [open, setOpen] = useState(false);

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
            <SheetTitle className="sr-only">Menu Admin</SheetTitle>
            <NavContent onNavigate={() => setOpen(false)} />
          </SheetContent>
        </Sheet>
        <div className="flex items-center gap-2">
          <ShieldCheck className="h-5 w-5 text-primary" />
          <span className="font-semibold text-foreground">Admin</span>
        </div>
        <div className="w-9" />
      </header>

      {/* Área principal de conteúdo */}
      <main className="lg:pl-64 min-h-screen">
        <Outlet />
      </main>
    </div>
  );
}
