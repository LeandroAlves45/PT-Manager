import { NavLink } from 'react-router';

import type { NavigationGroup } from '@/shared/config/navigation';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/shared/components/ui/tooltip';
import { cn } from '@/shared/lib/utils';

/**
 * Lista de navegação da sidebar.
 *
 * Quando recolhida (72 px) cada item fica reduzido ao ícone e ganha um tooltip. O tooltip
 * do Radix abre também por foco de teclado, o que é o que torna a sidebar recolhida
 * utilizável sem rato.
 */
export function SidebarNav({
  groups,
  collapsed,
  onNavigate,
}: {
  groups: readonly NavigationGroup[];
  collapsed: boolean;
  onNavigate?: () => void;
}) {
  return (
    <nav aria-label="Navegação principal" className="flex flex-col gap-4">
      {groups.map((group) => (
        <div key={group.label} className="flex flex-col gap-1">
          {!collapsed && (
            <p className="text-muted-foreground px-3 text-[11px] font-semibold tracking-wide uppercase">
              {group.label}
            </p>
          )}

          {group.items.map((item) => {
            const link = (
              <NavLink
                key={item.route}
                to={item.route}
                end={item.exact ?? false}
                onClick={onNavigate}
                className={({ isActive }) =>
                  cn(
                    'relative flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors duration-120',
                    'hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                    collapsed && 'justify-center px-0',
                    isActive &&
                      'bg-sidebar-primary/12 text-sidebar-foreground before:bg-sidebar-primary before:absolute before:top-1/2 before:left-0 before:h-5 before:w-0.5 before:-translate-y-1/2 before:rounded-full'
                  )
                }
              >
                <item.icon aria-hidden className="size-4 shrink-0" />
                {!collapsed && <span>{item.label}</span>}
                {collapsed && <span className="sr-only">{item.label}</span>}
              </NavLink>
            );

            if (!collapsed) {
              return link;
            }

            return (
              <Tooltip key={item.route} delayDuration={200}>
                <TooltipTrigger asChild>{link}</TooltipTrigger>
                <TooltipContent side="right">{item.label}</TooltipContent>
              </Tooltip>
            );
          })}
        </div>
      ))}
    </nav>
  );
}
