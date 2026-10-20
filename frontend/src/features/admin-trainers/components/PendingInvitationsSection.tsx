import {
  usePendingInvitationsQuery,
  type PendingInvitation,
} from '@/features/admin-trainers/api/trainers';
import { isInvitationExpired } from '@/features/admin-trainers/lib/trainer-labels';
import { ErrorState } from '@/shared/components/ErrorState';
import { Button } from '@/shared/components/ui/button';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatLisbonDateTime } from '@/shared/lib/format';

/**
 * Convites de treinador por usar, incluindo os que já expiraram, com reenviar e anular.
 *
 * @param busy Há um reenvio a decorrer (impede um segundo clique enquanto o primeiro corre).
 * @param onResend Reenvia o convite (o link anterior deixa de servir).
 * @param onRevoke Abre a confirmação de anular o convite.
 */
export function PendingInvitationsSection({
  busy,
  onResend,
  onRevoke,
}: {
  busy: boolean;
  onResend: (invitation: PendingInvitation) => void;
  onRevoke: (invitation: PendingInvitation) => void;
}) {
  const query = usePendingInvitationsQuery();
  const items = query.data ?? [];

  return (
    <section aria-labelledby="pending-invitations-title" className="space-y-3">
      <h2 id="pending-invitations-title" className="font-display text-xl">
        Convites pendentes
      </h2>

      {query.isPending ? (
        <div role="status" aria-label="A carregar convites…" className="space-y-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : query.isError ? (
        <ErrorState error={query.error} onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        <p className="text-muted-foreground text-sm">Não há convites pendentes.</p>
      ) : (
        <div className="border-border bg-card overflow-x-auto rounded-xl border">
          <table className="w-full min-w-2xl text-sm">
            <thead className="bg-muted text-left">
              <tr>
                <th scope="col" className="p-3">
                  Email
                </th>
                <th scope="col" className="p-3">
                  Enviado a
                </th>
                <th scope="col" className="p-3">
                  Válido até
                </th>
                <th scope="col" className="p-3">
                  Ações
                </th>
              </tr>
            </thead>
            <tbody>
              {items.map((invitation) => (
                <tr key={invitation.invitation_id} className="border-border border-t">
                  <th scope="row" className="p-3 text-left font-medium break-all">
                    {invitation.email}
                  </th>
                  <td className="p-3">{formatLisbonDateTime(invitation.sent_at)}</td>
                  <td className="p-3">
                    {isInvitationExpired(invitation.expires_at)
                      ? 'Expirou'
                      : formatLisbonDateTime(invitation.expires_at)}
                  </td>
                  <td className="flex flex-wrap gap-1 p-2">
                    <Button
                      size="sm"
                      variant="outline"
                      className="min-h-11 md:min-h-8"
                      disabled={busy}
                      aria-label={`Enviar outro a ${invitation.email}`}
                      onClick={() => onResend(invitation)}
                    >
                      Enviar outro
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="min-h-11 md:min-h-8"
                      aria-label={`Anular convite a ${invitation.email}`}
                      onClick={() => onRevoke(invitation)}
                    >
                      Anular
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
