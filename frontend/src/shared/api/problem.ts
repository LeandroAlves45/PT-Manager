import type { components } from '@/shared/api/schema';

/**
 * Tradução de `application/problem+json` num erro tipado.
 *
 * O backend responde sempre com a mesma forma: `title` é o código
 * estável do erro, `detail` a descrição, `correlation_id` liga o pedido aos logs e, na
 * categoria de validação, `errors[]` traz um item por campo.
 */

type ApiProblemDetails = components['schemas']['ApiProblemDetails'];

/** Erro de validação associado a um campo de pedido. */
export interface ApiFieldError {
  readonly field: string;
  readonly code: string;
  readonly message: string;
}

/** Erro da API já normalizado para consumo da UI. */
export class ApiProblem extends Error {
  // Código HTTP da resposta
  readonly status: number;

  // Código estável do erro ('title' do ProblemDetails)
  readonly code: string;

  // Identificador para correlacionar com os logs do servidor
  readonly correlationId: string | null;

  // Erros por campo; vazio quando a falha não é a validação
  readonly fieldErrors: readonly ApiFieldError[];

  constructor(init: {
    status: number;
    code: string;
    detail: string;
    correlationId: string | null;
    fieldErrors: readonly ApiFieldError[];
  }) {
    super(init.detail);
    this.name = 'ApiProblem';
    this.status = init.status;
    this.code = init.code;
    this.correlationId = init.correlationId;
    this.fieldErrors = init.fieldErrors;
  }

  /** Verdadeiro quando há erros por campo para devolver ao formulário. */
  get hasFieldErrors(): boolean {
    return this.fieldErrors.length > 0;
  }
}

/**
 * Constrói um `ApiProblem` a partir do corpo da resposta.
 *
 * Aceita um corpo em falta ou malformado: uma resposta 503 de um proxy não traz
 * ProblemDetails nenhum, e mesmo assim a UI tem de mostrar algo coerente.
 *
 * @param status Código HTTP recebido.
 * @param body Corpo da resposta, quando foi possível interpretá-lo como JSON.
 */
export function toApiProblem(status: number, body: unknown): ApiProblem {
  const problem = (body ?? {}) as Partial<ApiProblemDetails>;

  const fieldErrors: ApiFieldError[] = Array.isArray(problem.errors)
    ? problem.errors.map((error) => ({
        field: error.field ?? '',
        code: error.code ?? '',
        message: error.message ?? '',
      }))
    : [];

  return new ApiProblem({
    status,
    code: problem.title ?? `http_${status}`,
    detail: problem.detail ?? 'Ocorreu um erro inesperado.',
    correlationId: problem.correlation_id ?? null,
    fieldErrors,
  });
}

/** Estreita um 'unknown' apanhado num 'catch' para um 'ApiProblem'. */
export function isApiProblem(error: unknown): error is ApiProblem {
  return error instanceof ApiProblem;
}
