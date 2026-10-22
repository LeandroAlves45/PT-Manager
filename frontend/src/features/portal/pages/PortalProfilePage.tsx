import { usePortalProfileQuery } from '@/features/portal/api/portal';
import { AvatarSection } from '@/features/portal/components/AvatarSection';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { PortalUnavailable } from '@/features/portal/components/PortalUnavailable';
import { ProfileContactsForm } from '@/features/portal/components/ProfileContactsForm';
import { StaleDataNotice } from '@/features/portal/components/StaleDataNotice';
import { PROFILE_SEX_LABELS } from '@/features/portal/lib/profile';
import { isApiProblem } from '@/shared/api/problem';
import { ErrorState } from '@/shared/components/ErrorState';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { formatDate } from '@/shared/lib/format';

/**
 * Perfil do cliente (`/portal/profile`): fotografia, dados da ficha e dados editáveis.
 *
 * Um `GET /portal/my-profile` alimenta o ecrã, e é o mesmo pedido (em cache) que o menu do
 * topo usa. O cliente edita o nome, o telefone e o contacto de emergência; a data de
 * nascimento e o sexo são só de leitura (quem os altera é o personal trainer). O email da
 * ficha não aparece em lado nenhum.
 */
export function PortalProfilePage() {
  const query = usePortalProfileQuery();

  if (query.isPending)
    return (
      <section className="mx-auto w-full max-w-xl space-y-4">
        <Skeleton className="h-10 w-32" />
        <div role="status" aria-label="A carregar o perfil…" className="space-y-3">
          <Skeleton className="h-32" />
          <Skeleton className="h-28" />
          <Skeleton className="h-72" />
        </div>
      </section>
    );

  // Ficha arquivada ou relação terminada: vale mesmo com dados em cache.
  if (isApiProblem(query.error) && query.error.code === 'portal_profile_not_available')
    return <PortalUnavailable />;

  if (query.data === undefined)
    return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;

  const profile = query.data;

  return (
    <section className="mx-auto w-full max-w-xl space-y-4">
      <h1 className="font-display pe-1 text-3xl leading-none">Perfil</h1>

      {query.isError && (
        <StaleDataNotice onRetry={() => void query.refetch()} retrying={query.isFetching} />
      )}

      <AvatarSection profile={profile} />

      <PortalCard title="Dados da ficha">
        <p className="text-muted-foreground mb-2 text-sm">
          Para alterar a data de nascimento ou o sexo, fala com o teu personal trainer.
        </p>
        <dl className="divide-border divide-y text-sm">
          <div className="flex justify-between gap-3 py-2">
            <dt className="text-muted-foreground">Data de nascimento</dt>
            <dd className="tabular-nums">{formatDate(profile.birth_date)}</dd>
          </div>
          <div className="flex justify-between gap-3 py-2">
            <dt className="text-muted-foreground">Sexo</dt>
            <dd>{PROFILE_SEX_LABELS[profile.sex] ?? profile.sex}</dd>
          </div>
        </dl>
      </PortalCard>

      {/* Sem `key` pelo perfil: enviar a fotografia muda o perfil e apagava o que o cliente
          estivesse a escrever nos contactos. */}
      <ProfileContactsForm profile={profile} />
    </section>
  );
}
