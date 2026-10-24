/** Campos comuns às três respostas (`ExerciseResponse`, `FoodResponse`, `SupplementResponse`). */
export interface LibraryItem {
  readonly id: string;
  readonly name: string;
  readonly scope: string;
  readonly is_active: boolean;
  /** Estado da moderação da plataforma; `blocked` é um item bloqueado. */
  readonly platform_enforcement_status: string;
  /** Código do motivo do bloqueio (`null` enquanto não está bloqueado). */
  readonly platform_enforcement_reason: string | null;
  /** Nota do superuser sobre o bloqueio, quando existe. */
  readonly platform_enforcement_note: string | null;
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
