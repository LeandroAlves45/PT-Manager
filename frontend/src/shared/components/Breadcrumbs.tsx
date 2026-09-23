import { ChevronRight } from 'lucide-react';
import { Link, useLocation } from 'react-router';

import { cn } from '@/shared/lib/utils';

/**
 * Migalhas geradas a partir do caminho atual.
 *
 * O último nível nunca é ligação: já estamos nele. Os segmentos que são identificadores
 * (UUID) aparecem como "Detalhe" — mostrar um UUID ao utilizador não ajuda ninguém.
 */

const SEGMENT_LABELS: Record<string, string> = {
  admin: 'Plataforma',
  trainer: 'Painel',
  portal: 'Portal',
  clients: 'Clientes',
  sessions: 'Sessões e packs',
  'check-ins': 'Check-ins',
  'training-plans': 'Planos de treino',
  'meal-plans': 'Planos alimentares',
  library: 'Biblioteca',
  settings: 'Marca própria',
  billing: 'Subscrição',
  moderation: 'Moderação',
  catalog: 'Catálogos',
  foods: 'Alimentos',
  exercises: 'Exercícios',
  supplements: 'Suplementos',
  today: 'Treino de hoje',
  nutrition: 'Nutrição',
  profile: 'Perfil',
};

const IDENTIFIER = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function labelFor(segment: string): string {
  if (IDENTIFIER.test(segment)) {
    return 'Detalhe';
  }

  return SEGMENT_LABELS[segment] ?? segment.replace(/-/g, ' ');
}

/** Caminho de navegação da topbar. */
export function Breadcrumbs({ className }: { className?: string }) {
  const { pathname } = useLocation();
  const segments = pathname.split('/').filter(Boolean);

  if (segments.length === 0) return null;

  return (
    <nav aria-label="Caminho" className={cn('min-w-0', className)}>
      <ol className="text-muted-foreground flex items-center gap-1 text-sm">
        {segments.map((segment, index) => {
          const isLast = index === segments.length - 1;
          const route = `/${segments.slice(0, index + 1).join('/')}`;

          return (
            <li key={route} className="flex min-w-0 items-center gap-1">
              {index > 0 && <ChevronRight aria-hidden className="size-3.5 shrink-0 opacity-60" />}
              {isLast ? (
                <span aria-current="page" className="text-foreground overflow-hidden font-medium">
                  {labelFor(segment)}
                </span>
              ) : (
                <Link to={route} className="hover:text-foreground transition-colors">
                  {labelFor(segment)}
                </Link>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
