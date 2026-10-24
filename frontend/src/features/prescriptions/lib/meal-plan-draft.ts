import { useOutletContext } from 'react-router';

import type { ClientChoice } from '@/features/clients';
import type { BlockedItems } from '@/features/prescriptions/lib/blocked-items';
import type {
  Calculation,
  CalculationResult,
  FoodNutrients,
} from '@/features/prescriptions/lib/nutrition';
import type { components } from '@/shared/api/schema';

/** Estrutura de refeições do pedido de criação e de atualização. */
export type MealStructure = components['schemas']['MealPlanStructureRequest'];

/**
 * Rascunho de um plano alimentar. Vive em memória no layout do editor e é partilhado pelos
 * dois passos; só chega ao servidor no fim, num único pedido (U57).
 */
export interface MealPlanDraft {
  readonly client: ClientChoice | null;
  readonly name: string;
  readonly description: string;
  readonly startsDate: string;
  readonly endsDate: string;
  readonly calculation: Calculation;
  /** `true` quando o cálculo mudou desde o último preview ou snapshot gravado. */
  readonly calculationDirty: boolean;
  /** Metas do cálculo atual; `null` até «Calcular» (ou depois de o cálculo mudar). */
  readonly preview: CalculationResult | null;
  readonly structure: MealStructure;
}

/** Os dois passos do editor, na ordem do fluxo. */
export type MealPlanStep = 'targets' | 'meals';

/** O que o layout entrega aos passos por `useOutletContext`. */
export interface MealPlanEditorContext {
  /** Plano a editar; `null` num plano novo. */
  readonly planId: string | null;
  /** Plano arquivado: só consulta. */
  readonly readOnly: boolean;
  readonly draft: MealPlanDraft;

  /** `true` quando o cliente vem de `?client_id=` ou do plano: não se escolhe. */
  readonly clientFixed: boolean;
  /** Nome do cliente, quando já se conhece. */
  readonly clientName: string;
  /** Idade do cliente em anos completos; `null` sem cliente ou enquanto a ficha carrega. */
  readonly clientAge: number | null;
  readonly clientLoading: boolean;
  readonly clientError: unknown;
  readonly retryClient: () => void;
  /** «Usar avaliação inicial» só se oferece com a ficha do cliente carregada. */
  readonly canUseAssessment: boolean;

  readonly setField: (
    field: 'name' | 'description' | 'startsDate' | 'endsDate',
    value: string
  ) => void;
  readonly chooseClient: (client: ClientChoice) => void;
  readonly changeCalculation: (next: Calculation) => void;
  readonly applyAssessment: () => void;
  readonly changeStructure: (next: MealStructure) => void;

  /** Valores por 100 g dos alimentos do rascunho, por ID do catálogo. */
  readonly foods: Readonly<Record<string, FoodNutrients>>;
  /** Unidade de medida dos suplementos do rascunho, por ID do catálogo. */
  readonly units: Readonly<Record<string, string>>;
  /** Nomes dos alimentos e suplementos do rascunho, por ID do catálogo. */
  readonly names: Readonly<Record<string, string>>;
  /** Regista o nome de um alimento ou suplemento acabado de escolher. */
  readonly nameItem: (id: string, name: string) => void;
  /** Linhas do plano gravado cujo item a moderação bloqueou: ficam fora dos totais. */
  readonly blocked: BlockedItems;

  /** Há preview do cálculo atual (ou o snapshot gravado continua válido). */
  readonly hasValidTargets: boolean;
  readonly runPreview: () => void;
  readonly previewPending: boolean;
  readonly previewError: string;

  readonly save: () => void;
  readonly savePending: boolean;
  readonly saveError: string;

  /** Caminho (com a query string) do passo das metas, para redirecionar. */
  readonly targetsPath: string;
  /** Navega entre passos sem acionar o aviso de alterações por guardar. */
  readonly goToStep: (step: MealPlanStep) => void;
}

/** Contexto do editor, para os passos que o layout renderiza em `<Outlet />`. */
export function useMealPlanEditor(): MealPlanEditorContext {
  return useOutletContext<MealPlanEditorContext>();
}

/** Destino de «Voltar» e de depois de criar: a ficha do cliente (separador Nutrição) ou a lista. */
export function mealPlanBackPath(clientId: string | null): string {
  return clientId === null ? '/trainer/meal-plans' : `/trainer/clients/${clientId}?tab=nutrition`;
}
