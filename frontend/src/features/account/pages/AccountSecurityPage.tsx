import { useAccountQuery } from '@/features/account/api/account';
import { ChangePasswordForm } from '@/features/account/components/ChangePasswordForm';
import { GoogleLinkCard } from '@/features/account/components/GoogleLinkCard';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * Conta e segurança (`/{admin,trainer,portal}/account`), igual para os três papéis.
 *
 * Um `GET /auth/account` decide o que se mostra:
 * - com palavra-passe: formulário para a mudar, e o cartão "Ligar conta Google" enquanto o
 *   Google não está ligado;
 * - só Google (`has_password: false`): explica como se entra e não oferece formulário, porque
 *   não há palavra-passe para mudar;
 * - Google já ligado: um aviso simples (desligar fica fora do âmbito).
 */
export function AccountSecurityPage() {
  const query = useAccountQuery();

  if (query.isPending)
    return (
      <section className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <div role="status" aria-label="A carregar a conta…" className="space-y-3">
          <Skeleton className="h-20" />
          <Skeleton className="h-72" />
        </div>
      </section>
    );

  if (query.data === undefined)
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;

  const account = query.data;

  return (
    <section className="max-w-2xl space-y-4">
      <PageHeader title="Conta e segurança" description="Gere a forma como entras na tua conta." />

      <div className="border-border bg-card rounded-lg border p-4">
        <p className="text-muted-foreground text-xs">Email da conta</p>
        <p className="text-sm font-medium break-all">{account.email}</p>
      </div>

      {account.has_password ? (
        <ChangePasswordForm />
      ) : (
        <p className="border-border bg-card rounded-lg border p-4 text-sm">
          Entras com a conta Google ({account.email}). Não tens palavra-passe para mudar.
        </p>
      )}

      {account.google_linked ? (
        <p role="status" className="border-border bg-card rounded-lg border p-4 text-sm">
          Conta Google ligada.
        </p>
      ) : (
        account.has_password && <GoogleLinkCard />
      )}
    </section>
  );
}
