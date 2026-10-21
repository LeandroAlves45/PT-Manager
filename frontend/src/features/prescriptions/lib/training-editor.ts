import { useOutletContext } from 'react-router';

import type { ClientChoice } from '@/features/clients';
import type { DetailsErrors, TrainingStructure } from '@/features/prescriptions/lib/training-draft';

/** Rascunho do plano: o que os dois passos editam e uma só gravação envia. */
export interface TrainingDraft {
  client: ClientChoice | null;
  name: string;
  description: string;
  modality: string;
  notes: string;
  startDate: string;
  endDate: string;
  structure: TrainingStructure;
}

/** O que o layout entrega aos passos por `useOutletContext`. */
export interface TrainingEditorContext {
  mode: 'create' | 'edit';
  /** Caminho do passo 1 (sem a query string). */
  basePath: string;
  /** `?client_id=...` ou `''`, para os links entre passos o conservarem. */
  search: string;
  /** O cliente veio da ficha (`?client_id=`) ou é o do plano existente: não se escolhe. */
  clientLocked: boolean;
  draft: TrainingDraft;
  detailsErrors: DetailsErrors;
  patch: (change: Partial<Omit<TrainingDraft, 'structure'>>) => void;
  changeStructure: (change: (structure: TrainingStructure) => void) => void;
  exerciseNames: Readonly<Record<string, string>>;
  rememberExercise: (id: string, label: string) => void;
  /** Plano arquivado: só consulta. */
  readOnly: boolean;
  /** O plano já tem histórico: estrutura e datas são só de consulta. */
  hasHistory: boolean;
  canChangeStructure: boolean;
  canChangeDates: boolean;
  /** Grava o rascunho (POST, PUT ou, com histórico, PATCH só de metadados). */
  save: () => void;
  saving: boolean;
  /** Mensagem de uma falha de gravação; os passos podem limpá-la. */
  /** Erro da última tentativa de guardar, mostrado junto aos botões do passo; '' sem erro. */
  error: string;
  setError: (message: string) => void;
}

/** Contexto do layout do editor; só válido dentro de `TrainingPlanEditorLayout`. */
export function useTrainingEditor(): TrainingEditorContext {
  return useOutletContext<TrainingEditorContext>();
}
