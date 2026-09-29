/**
 * API pública da feature sessions.
 *
 * O painel usa os rótulos, os erros e a ação "Registar presença"; o detalhe do cliente monta
 * a tab Sessões. A página entra no router pelo caminho, como as das outras features.
 */
export { sessionKeys } from '@/features/sessions/api/keys';
export { useSessionTransitionMutation } from '@/features/sessions/api/mutations';
export { ClientSessionsTab } from '@/features/sessions/components/ClientSessionsTab';
export {
  SESSION_ERRORS,
  SESSION_STATUS_LABELS,
  TRANSITION_SUCCESS,
} from '@/features/sessions/lib/sessionStatus';
