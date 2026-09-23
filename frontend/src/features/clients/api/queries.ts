import { useQuery } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import { clientKeys } from '@/features/clients/api/keys';

/** Número máximo de clientes mostrados na paleta de comandos. */
const SEARCH_PAGE_SIZE = 5;

/** Mínimo de caracteres antes de valer a pena perguntar ao servidor. */
const MIN_SEARCH_LENGTH = 2;

/**
 * Pesquisa clientes por nome para a paleta de comandos.
 *
 * Orçamento de pedidos: um `GET /api/v1/clients` por termo estabilizado, com
 * `page_size` de 5. O atraso de 300 ms é responsabilidade de quem chama (`useDebounce`),
 * para que a chave da query já seja o termo final e o cache funcione.
 *
 * @param term Termo já estabilizado. Abaixo de dois caracteres não há pedido nenhum.
 */
export function useClientSearchQuery(term: string) {
  const enabled = term.trim().length >= MIN_SEARCH_LENGTH;

  return useQuery({
    queryKey: clientKeys.search(term),
    enabled,
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/clients', {
          params: { query: { search: term, page_number: 1, page_size: SEARCH_PAGE_SIZE } },
          signal,
        })
        .then(unwrap),
  });
}
