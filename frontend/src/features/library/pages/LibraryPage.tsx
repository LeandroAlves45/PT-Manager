import { parseAsInteger, parseAsString, parseAsStringLiteral, useQueryStates } from 'nuqs';

import type { LibraryKind } from '@/features/library/api/keys';
import { ExercisesPanel } from '@/features/library/components/ExercisesPanel';
import { FoodsPanel } from '@/features/library/components/FoodsPanel';
import { SupplementsPanel } from '@/features/library/components/SupplementsPanel';
import { ACTIVITY_OPTIONS, LIBRARY_TEXTS } from '@/features/library/lib/labels';
import type { LibraryControls } from '@/features/library/lib/useLibraryFilters';
import { PageHeader } from '@/shared/components/PageHeader';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/components/ui/tabs';

const TABS = ['exercises', 'foods', 'supplements'] as const satisfies readonly LibraryKind[];

const urlParsers = {
  tab: parseAsStringLiteral(TABS).withDefault('exercises'),
  search: parseAsString.withDefault(''),
  activity: parseAsStringLiteral(ACTIVITY_OPTIONS).withDefault('active'),
  page: parseAsInteger.withDefault(1),
};

/**
 * Biblioteca do personal trainer: exercícios (com vídeo), alimentos e suplementos, globais
 * e privados, num separador cada.
 *
 * Tudo o que define a lista vive no URL (F1): `?tab=foods&search=frango&activity=all&page=2`
 * sobrevive a F5 e partilha-se. Trocar de separador limpa pesquisa, estado e página, porque
 * uma pesquisa de exercícios não faz sentido nos alimentos. Os valores por omissão não
 * aparecem no URL.
 */
export function LibraryPage() {
  const [url, setUrl] = useQueryStates(urlParsers);
  const page = Math.max(1, url.page);

  const controls: LibraryControls = {
    search: url.search,
    activity: url.activity,
    page,
    setSearch: (value) => void setUrl({ search: value === '' ? null : value, page: null }),
    setActivity: (value) =>
      void setUrl({ activity: value === 'active' ? null : value, page: null }),
    setPage: (next) => void setUrl({ page: next === 1 ? null : next }),
    clear: () => void setUrl({ search: null, activity: null, page: null }),
  };

  return (
    <section className="space-y-6">
      <PageHeader
        title="Biblioteca"
        description="Exercícios, alimentos e suplementos: os globais da plataforma e os teus privados."
      />

      <Tabs
        value={url.tab}
        onValueChange={(value) => {
          const next = TABS.find((item) => item === value) ?? 'exercises';
          void setUrl({
            tab: next === 'exercises' ? null : next,
            search: null,
            activity: null,
            page: null,
          });
        }}
      >
        <TabsList>
          {TABS.map((tab) => (
            <TabsTrigger key={tab} value={tab}>
              {LIBRARY_TEXTS[tab].tab}
            </TabsTrigger>
          ))}
        </TabsList>
        <TabsContent value="exercises">
          <ExercisesPanel controls={controls} />
        </TabsContent>
        <TabsContent value="foods">
          <FoodsPanel controls={controls} />
        </TabsContent>
        <TabsContent value="supplements">
          <SupplementsPanel controls={controls} />
        </TabsContent>
      </Tabs>
    </section>
  );
}
