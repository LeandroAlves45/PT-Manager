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
  'check-ins': 'Avaliações',
  'training-plans': 'Planos de treino',
  'meal-plans': 'Planos alimentares',
  'supplement-assignments': 'Suplementos atribuídos',
  library: 'Biblioteca',
  settings: 'Marca própria',
  billing: 'Subscrição',
  moderation: 'Moderação',
  trainers: 'Treinadores',
  invite: 'Convidar',
  new: 'Criar',
  edit: 'Editar',
  structure: 'Estrutura',
  meals: 'Refeições',
  'initial-assessment': 'Avaliação inicial',
  'sell-pack': 'Vender pack',
  'pack-types': 'Tipos de pack',
  reschedule: 'Reagendar',
  'set-logs': 'Séries realizadas',
  catalog: 'Catálogos',
  foods: 'Alimentos',
  exercises: 'Exercícios',
  supplements: 'Suplementos',
  today: 'Treino de hoje',
  nutrition: 'Nutrição',
  profile: 'Perfil',
  account: 'Conta e segurança',
};

const IDENTIFIER = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function labelFor(segment: string): string {
  if (IDENTIFIER.test(segment)) {
    return 'Detalhe';
  }

  return SEGMENT_LABELS[segment] ?? segment.replace(/-/g, ' ');
}

/**
 * Caminho de navegação da topbar.
 *
 * @param inheritColor `true` quando a topbar tem a cor de fundo da marca do personal trainer: o
 *   texto herda a cor já validada para AA em vez dos tons cinzentos do tema.
 */
export function Breadcrumbs({
  className,
  inheritColor = false,
}: {
  className?: string;
  inheritColor?: boolean;
}) {
  const { pathname, search } = useLocation();
  // Um editor aberto a partir da ficha do cliente leva `?client_id=`: as migalhas dentro da
  // secção (passos, detalhe do plano) conservam-no, para o cliente continuar fixo e o regresso
  // continuar a ser a ficha. As migalhas das listas e do painel não precisam dele.
  const clientId = new URLSearchParams(search).get('client_id');
  const clientSearch = clientId === null ? '' : `?client_id=${encodeURIComponent(clientId)}`;
  const segments = pathname.split('/').filter(Boolean);

  if (segments.length === 0) return null;

  return (
    <nav aria-label="Caminho" className={cn('min-w-0', className)}>
      <ol
        className={cn(
          'flex items-center gap-1 text-sm',
          inheritColor ? 'text-current' : 'text-muted-foreground'
        )}
      >
        {segments.map((segment, index) => {
          const isLast = index === segments.length - 1;
          const route = `/${segments.slice(0, index + 1).join('/')}`;
          const to = index >= 2 ? `${route}${clientSearch}` : route;

          return (
            <li
              key={route}
              className={cn('min-w-0 items-center gap-1', isLast ? 'flex' : 'hidden lg:flex')}
            >
              {index > 0 && (
                <ChevronRight
                  aria-hidden
                  className="hidden size-3.5 shrink-0 opacity-60 lg:block"
                />
              )}
              {isLast ? (
                <span
                  aria-current="page"
                  className={cn(
                    'overflow-hidden font-medium',
                    inheritColor ? 'text-current' : 'text-foreground'
                  )}
                >
                  {labelFor(segment)}
                </span>
              ) : (
                <Link
                  to={to}
                  className={cn(
                    'transition-colors',
                    inheritColor ? 'hover:underline' : 'hover:text-foreground'
                  )}
                >
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
