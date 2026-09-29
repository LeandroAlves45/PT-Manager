import { useState } from 'react';

import { useClientSearchQuery } from '@/features/clients/api/queries';
import { Combobox, type ComboboxOption } from '@/shared/components/Combobox';
import type { FieldControlProps } from '@/shared/components/FormField';
import { useDebounce } from '@/shared/hooks/useDebounce';

/** Cliente escolhido: só o que os formulários precisam de guardar e mostrar. */
export interface ClientChoice {
  readonly id: string;
  readonly name: string;
}

/**
 * Escolha de cliente com pesquisa por nome, para formulários fora do detalhe do cliente
 * (marcar sessão, atribuir pack).
 *
 * Orçamento de pedidos: o mesmo `GET /clients?search=` da paleta de comandos (5 resultados,
 * mínimo 2 caracteres, debounce de 300 ms), e por isso a mesma entrada de cache. Sem
 * `activity`, o backend devolve só clientes ativos — os arquivados não podem ter sessões
 * novas (`session_client_inactive`) nem packs novos (`client_inactive`).
 */
export function ClientCombobox({
  value,
  onChange,
  ...control
}: Partial<FieldControlProps> & {
  value: ClientChoice | null;
  onChange: (client: ClientChoice) => void;
}) {
  const [term, setTerm] = useState('');
  const debounced = useDebounce(term.trim(), 300);
  const query = useClientSearchQuery(debounced);
  const options: ComboboxOption[] = (query.data?.items ?? []).map((client) => ({
    value: client.id,
    label: client.name,
    description: client.phone,
  }));

  return (
    <Combobox
      {...control}
      value={value === null ? null : { value: value.id, label: value.name }}
      onChange={(option) => onChange({ id: option.value, name: option.label })}
      search={term}
      onSearchChange={setTerm}
      options={options}
      loading={query.isFetching}
      placeholder="Escolhe um cliente…"
      searchPlaceholder="Pesquisar por nome"
      emptyText={
        debounced.length < 2 ? 'Escreve pelo menos 2 letras.' : 'Nenhum cliente ativo encontrado.'
      }
    />
  );
}
