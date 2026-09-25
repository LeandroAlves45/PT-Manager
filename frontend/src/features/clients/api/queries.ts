import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import { clientKeys, type ClientListFilters } from '@/features/clients/api/keys';

/** Número máximo de clientes mostrados na paleta de comandos. */
const SEARCH_PAGE_SIZE = 5;

/** Mínimo de caracteres antes de valer a pena perguntar ao servidor. */
const MIN_SEARCH_LENGTH = 2;

/** Tamanho de página da tabela de clientes. */
export const CLIENT_PAGE_SIZE = 25;

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

/**
 * Página da tabela de clientes.
 *
 * Orçamento de pedidos: um `GET /api/v1/clients` por combinação de filtros; itens e total
 * vêm na mesma resposta, nunca um pedido por linha. `keepPreviousData` mantém a tabela
 * visível ao mudar de página ou filtro, em vez de piscar para skeleton.
 */
export function useClientListQuery(filters: ClientListFilters) {
  return useQuery({
    queryKey: clientKeys.list(filters),
    placeholderData: keepPreviousData,
    queryFn: async ({ signal }) =>
      unwrap(
        await apiClient.GET('/api/v1/clients', {
          params: {
            query: {
              activity: filters.activity,
              search: filters.search || undefined,
              page_number: filters.page,
              page_size: CLIENT_PAGE_SIZE,
            },
          },
          signal,
        })
      ),
  });
}

/** Ficha completa do cliente ('GET /api/v1/clients/{clientId}'), inclui packs utilizáveis. */
export function useClientQuery(clientId: string) {
  return useQuery({
    queryKey: clientKeys.detail(clientId),
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/clients/{clientId}', { params: { path: { clientId } }, signal })
        .then(unwrap),
  });
}

/** Resumo de progresso do separador "Resumo" (peso, adesão, metas, packs). */
export function useClientSummaryQuery(clientId: string) {
  return useQuery({
    queryKey: clientKeys.summary(clientId),
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/clients/{clientId}/summary', { params: { path: { clientId } }, signal })
        .then(unwrap),
  });
}

/**
 * Avaliação inicial do cliente, ou `null` quando ainda não existe.
 *
 * O backend responde 404 tanto para "sem avaliação" como para "cliente de outro tenant";
 * este hook só corre depois de a ficha ter carregado com sucesso, por isso aqui um 404
 * significa "ainda sem avaliação" e não é erro de ecrã.
 */
export function useInitialAssessmentQuery(clientId: string, enabled: boolean) {
  return useQuery({
    queryKey: clientKeys.initialAssessment(clientId),
    enabled,
    queryFn: async ({ signal }) => {
      try {
        return unwrap(
          await apiClient.GET('/api/v1/clients/{clientId}/initial-assessment', {
            params: { path: { clientId } },
            signal,
          })
        );
      } catch (error) {
        if (isApiProblem(error) && error.status === 404) return null;
        throw error;
      }
    },
  });
}
