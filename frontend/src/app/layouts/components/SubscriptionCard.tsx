import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';

import { apiClient, unwrap } from '@/shared/api/client';
import { Progress } from '@/shared/components/ui/progress';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatNumber } from '@/shared/lib/format';

/**
 * Cartão de subscrição no rodapé da sidebar do personal trainer.
 *
 * O mockup mostrava "18 de 25 clientes" fixo. A realidade do backend
 * (`GET /billing/subscription`) é outra: no plano PRO `client_limit` vem `null` e não há
 * barra nenhuma para desenhar.
 */
export function SubscriptionCard() {
  const { data, isPending, isError } = useQuery({
    queryKey: ['billing', 'subscription'],
    queryFn: () => apiClient.GET('/api/v1/billing/subscription').then(unwrap),
  });

  if (isPending) return <Skeleton className="h-20 w-full" />;

  // Um erro aqui não pode partir a navegação: o cartão simplesmente não aparece.
  if (isError) return null;

  // 'client_limit' nulo é o plano PRO: sem limite, logo sem barra de progresso.
  const clientLimit = data.client_limit;
  const percentage =
    clientLimit === null
      ? 0
      : Math.min(100, Math.round((data.current_client_count / Math.max(clientLimit, 1)) * 100));

  return (
    <Link
      to="/trainer/billing"
      className="border-sidebar-border bg-sidebar-accent/50 hover:border-sidebar-ring/40 block rounded-xl border p-3 transition-colors"
    >
      <p className="font-display text-sidebar-foreground text-sm">Plano {data.tier}</p>

      <p className="text-muted-foreground tabular mt-1 text-xs">
        {clientLimit === null
          ? `${formatNumber(data.current_client_count)} clientes · ilimitado`
          : `${formatNumber(data.current_client_count)} de ${formatNumber(clientLimit)} clientes`}
      </p>

      {clientLimit !== null && <Progress value={percentage} className="mt-2 h-1.5" />}

      {data.status !== 'active' && data.status !== 'trialing' && (
        <p className="text-warning mt-2 text-xs font-medium">Pagamento por regularizar</p>
      )}
    </Link>
  );
}
