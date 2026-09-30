/** Campos comuns às três respostas (`ExerciseResponse`, `FoodResponse`, `SupplementResponse`). */
export interface LibraryItem {
  readonly id: string;
  readonly name: string;
  readonly scope: string;
  readonly is_active: boolean;
  // Ausente nos suplementos (a moderação só cobre exercícios e alimentos)
  readonly platform_enforcement_status?: string;
}

/**
 * Só um item privado, ativo e não bloqueado se edita. Um global é só de leitura
 * (403 no servidor), um suplemento arquivado dá 409 e um item bloqueado espera pela moderação.
 */
export function isEditable(item: LibraryItem): boolean {
  return (
    item.scope === 'private' && item.is_active && item.platform_enforcement_status !== 'blocked'
  );
}
