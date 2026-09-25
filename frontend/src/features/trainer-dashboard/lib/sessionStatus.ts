/**
 * Rótulos PT-PT dos estados de sessão (`Domain/ValueObjects/SessionStatus.cs`).
 *
 * Vive aqui enquanto o painel é o único consumidor; a fatia 6E-2 (sessões) passa-o para a
 * feature `sessions` e o painel importa-o do `index.ts` dela — uma só fonte.
 */
export const SESSION_STATUS_LABELS: Readonly<Record<string, string>> = {
  scheduled: 'Agendada',
  completed: 'Realizada',
  cancelled_by_client: 'Cancelada pelo cliente',
  cancelled_by_trainer: 'Cancelada por ti',
  no_show: 'Faltou',
};

/** Mensagens dos conflitos de "Registar presença" ('SessionErrors'). */
export const COMPLETE_SESSION_ERRORS: Readonly<Record<string, string>> = {
  session_transition_too_early: 'Só podes registar a presença depois da hora de início.',
  session_pack_balance_unavailable: 'O pack associado já não tem sessões disponíveis.',
  session_invalid_state: 'A sessão já não está agendada. Atualiza o painel.',
};
