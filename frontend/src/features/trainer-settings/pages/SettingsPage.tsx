import { BrandingSection } from '@/features/trainer-settings/components/BrandingSection';
import { ContactsSection } from '@/features/trainer-settings/components/ContactsSection';
import { LogoSection } from '@/features/trainer-settings/components/LogoSection';
import { TimezoneSection } from '@/features/trainer-settings/components/TimezoneSection';
import { useTrainerSettingsQuery } from '@/features/trainer-settings/api/settings';
import { ErrorState } from '@/shared/components/ErrorState';
import { PageHeader } from '@/shared/components/PageHeader';
import { Skeleton } from '@/shared/components/ui/skeleton';

/**
 * "Marca própria": definições do personal trainer em quatro secções independentes (marca,
 * logo, contactos e fuso horário). Cada uma guarda só os seus campos (o backend tem um
 * endpoint por secção) e a resposta atualiza a cache das outras.
 *
 * Orçamento de pedidos: um `GET /trainer-settings` ao abrir; uma escrita por "Guardar".
 */
export function SettingsPage() {
  const settings = useTrainerSettingsQuery();

  return (
    <section className="space-y-6">
      <PageHeader
        title="Marca própria"
        description="Nome, cores e logo do portal dos teus clientes, contactos e fuso horário."
      />
      {settings.isPending ? (
        <div role="status" aria-label="A carregar definições…" className="space-y-4">
          <Skeleton className="h-56 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
      ) : settings.isError ? (
        <ErrorState error={settings.error} onRetry={() => void settings.refetch()} />
      ) : (
        <div className="space-y-6">
          <BrandingSection settings={settings.data} />
          <LogoSection settings={settings.data} />
          <ContactsSection settings={settings.data} />
          <TimezoneSection settings={settings.data} />
        </div>
      )}
    </section>
  );
}
