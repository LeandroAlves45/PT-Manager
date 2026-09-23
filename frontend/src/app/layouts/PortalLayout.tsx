import { NavLink, Outlet } from 'react-router';

import { ProfileMenu } from '@/app/layouts/components/ProfileMenu';
import { useAuth } from '@/app/providers/useAuth';
import { ThemeToggle } from '@/app/components/ThemeToggle';
import { navigationFor } from '@/shared/config/navigation';
import { cn } from '@/shared/lib/utils';

/**
 * Moldura do portal do cliente: mobile-first, com barra inferior de quatro itens.
 *
 * Medidas: barra de 64 px mais a safe-area do
 * iOS, alvos de toque de pelo menos 44 px, e o item activo assinalado por ícone, texto e
 * barra superior — nunca só por cor, que é o que exige a WCAG 2.2.
 */
export function PortalLayout() {
  const { session } = useAuth();

  if (session === null) return null;

  const items = navigationFor(session.role).flatMap((group) => group.items);

  return (
    <div className="bg-background flex min-h-dvh flex-col">
      <header className="border-border bg-background/72 sticky top-0 z-10 flex h-14 items-center justify-between border-b px-4 backdrop-blur-[14px]">
        <span className="font-display text-lg">PT Manager</span>
        <div className="flex items-center gap-1">
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
