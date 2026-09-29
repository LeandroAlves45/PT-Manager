import { CalendarPlus } from 'lucide-react';
import { parseAsStringLiteral, useQueryState } from 'nuqs';
import { useState } from 'react';

import { ClientPacksPanel, PackTypesPanel } from '@/features/packs';
import { DayAgenda } from '@/features/sessions/components/DayAgenda';
import { NewSessionSheet } from '@/features/sessions/components/NewSessionSheet';
import { SessionsList } from '@/features/sessions/components/SessionsList';
import { todayKey } from '@/features/sessions/lib/dates';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/components/ui/tabs';

const TABS = ['agenda', 'packs', 'types'] as const;
const VIEWS = ['day', 'list'] as const;
const VIEW_LABELS = { day: 'Dia', list: 'Lista' } as const;

/**
 * "Sessões e packs" do personal trainer: três separadores sincronizados com o URL
 * (`?tab=`), para o painel poder ligar diretamente a um deles ("Renovar packs" →
 * `?tab=packs`).
 *
 * - **Agenda:** um dia de cada vez (‹ Hoje ›) ou a lista paginada com filtros.
 * - **Packs dos clientes:** vender, fim previsto, cancelar.
 * - **Tipos de pack:** o catálogo do que se vende.
 *
 * Cada separador só pede os seus dados quando está aberto (o Radix não monta o conteúdo dos
 * separadores inativos).
 */
export function SessionsPage() {
  const [tab, setTab] = useQueryState('tab', parseAsStringLiteral(TABS).withDefault('agenda'));
  const [view, setView] = useState<(typeof VIEWS)[number]>('day');
  const [day, setDay] = useState(todayKey);
  const [creating, setCreating] = useState(false);

  return (
    <section className="space-y-6">
      <PageHeader
        title="Sessões e packs"
        description="Agenda, packs vendidos e tipos de pack."
        action={
          <Button onClick={() => setCreating(true)}>
            <CalendarPlus aria-hidden /> Marcar sessão
          </Button>
        }
      />

      <Tabs
        value={tab}
        onValueChange={(value) => {
          const next = TABS.find((item) => item === value) ?? 'agenda';
          void setTab(next === 'agenda' ? null : next);
        }}
      >
        <TabsList>
          <TabsTrigger value="agenda">Agenda</TabsTrigger>
          <TabsTrigger value="packs">Packs dos clientes</TabsTrigger>
          <TabsTrigger value="types">Tipos de pack</TabsTrigger>
        </TabsList>

        <TabsContent value="agenda" className="space-y-4">
          <div
            role="group"
            aria-label="Vista"
            className="border-border flex w-fit rounded-lg border p-1"
          >
            {VIEWS.map((value) => (
              <Button
                key={value}
                variant={view === value ? 'secondary' : 'ghost'}
                size="sm"
                className="min-h-11 md:min-h-8"
                aria-pressed={view === value}
                onClick={() => setView(value)}
              >
                {VIEW_LABELS[value]}
              </Button>
            ))}
          </div>
          {view === 'day' ? (
            <DayAgenda day={day} onDayChange={setDay} onNewSession={() => setCreating(true)} />
          ) : (
            <SessionsList clientId={null} />
          )}
        </TabsContent>
        <TabsContent value="packs">
          <ClientPacksPanel client={null} />
        </TabsContent>
        <TabsContent value="types">
          <PackTypesPanel />
        </TabsContent>
      </Tabs>

      <NewSessionSheet
        open={creating}
        onOpenChange={setCreating}
        client={null}
        {...(view === 'day' ? { defaultDate: day } : {})}
      />
    </section>
  );
}
