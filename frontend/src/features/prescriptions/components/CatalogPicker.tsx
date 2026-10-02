import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

import { libraryKeys } from '@/features/library';
import { apiClient, unwrap } from '@/shared/api/client';
import { Combobox, type ComboboxOption } from '@/shared/components/Combobox';
import { useDebounce } from '@/shared/hooks/useDebounce';

export type CatalogKind = 'exercises' | 'foods' | 'supplements';

export interface CatalogChoice extends ComboboxOption {
  /** Porção habitual do alimento, em gramas. Ausente nos outros catálogos. */
  readonly defaultServingGrams?: number | null;
  /** Dose do suplemento, tal como está no catálogo. */
  readonly servingSize?: string;
  /** Momento de toma do suplemento. */
  readonly timing?: string;
}

/**
 * Pesquisa exercícios, alimentos ou suplementos e devolve a ficha completa ao pai.
 *
 * O pedido só corre com 2 ou mais letras, depois de 300 ms sem escrever.
 * Limpar a caixa zera o termo logo. A chave fica sob `libraryKeys.kind`,
 * com o segmento `selector`, para uma escrita na biblioteca refrescar
 * esta lista sem se misturar com a listagem da biblioteca.
 *
 * `onChange` recebe o `CatalogChoice` ainda presente em `query.data`
 * (porção, dose e momento quando o catálogo os tiver).
 * Página 1, 25 itens, só ativos: isto não é configurável pelas props.
 */
export function CatalogPicker({
  kind,
  id,
  value,
  onChange,
  disabled = false,
}: {
  kind: CatalogKind;
  id?: string;
  value: CatalogChoice | null;
  onChange: (choice: CatalogChoice) => void;
  disabled?: boolean;
}) {
  const [search, setSearch] = useState('');
  const debounced = useDebounce(search.trim(), 300);
  // O debounce serve para escrever, não para apagar.
  const term = search.trim() === '' ? '' : debounced;
  const query = useQuery({
    queryKey: [...libraryKeys.kind(kind), 'selector', term, 'active', 1, 25],
    enabled: term.length >= 2,
    queryFn: async ({ signal }): Promise<CatalogChoice[]> => {
      const query = { search: term, activity: 'active' as const, page_number: 1, page_size: 25 };

      if (kind === 'exercises') {
        const page = unwrap(
          await apiClient.GET('/api/v1/exercises', { params: { query }, signal })
        );
        return page.items.map((item) => ({
          value: item.id,
          label: item.name,
          description: item.scope === 'global' ? 'Global' : 'Privado',
        }));
      }
      if (kind === 'foods') {
        const page = unwrap(await apiClient.GET('/api/v1/foods', { params: { query }, signal }));
        return page.items.map((item) => ({
          value: item.id,
          label: item.name,
          description:
            (item.scope === 'global' ? 'Global' : 'Privado') + ' · ' + item.kcal + ' kcal / 100 g',
          defaultServingGrams: item.default_serving_grams,
        }));
      }
      const page = unwrap(
        await apiClient.GET('/api/v1/supplements', { params: { query }, signal })
      );
      return page.items.map((item) => ({
        value: item.id,
        label: item.name,
        description: item.scope === 'global' ? 'Global' : 'Privado',
        servingSize: item.serving_size,
        timing: item.timing,
      }));
    },
  });

  return (
    <Combobox
      id={id}
      value={value}
      onChange={(option) => {
        const choice = query.data?.find((item) => item.value === option.value);
        if (choice !== undefined) onChange(choice);
      }}
      search={search}
      onSearchChange={setSearch}
      options={query.data ?? []}
      loading={query.isFetching}
      placeholder="Escolhe no catálogo…"
      searchPlaceholder="Pesquisar no catálogo"
      emptyText={term.length < 2 ? 'Escreve pelo menos 2 letras.' : 'Nenhum resultado.'}
      disabled={disabled}
    />
  );
}
