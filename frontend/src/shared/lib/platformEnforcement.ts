/**
 * Motivos de bloqueio de conteúdo privado pela moderação da plataforma
 * (`PlatformEnforcementReason` no backend), com o rótulo PT-PT de cada código.
 *
 * Fonte única : a fila de moderação do admin escolhe o motivo e a biblioteca do personal trainer
 * mostra-o no item bloqueado.
 */
export const PLATFORM_ENFORCEMENT_REASONS = {
  malicious_content: 'Conteúdo malicioso',
  dangerous_information: 'Informação perigosa',
  deliberately_false_information: 'Informação deliberadamente falsa',
  prohibited_content: 'Conteúdo proibido',
} as const;

export type PlatformEnforcementReason = keyof typeof PLATFORM_ENFORCEMENT_REASONS;

/** Rótulo de um motivo vindo da API; 'null' ou desconhecido -> "—" */
export function platformEnforcementReasonLabel(reason: string | null): string {
  return reason !== null && reason in PLATFORM_ENFORCEMENT_REASONS
    ? PLATFORM_ENFORCEMENT_REASONS[reason as PlatformEnforcementReason]
    : '—';
}
