-- =============================================================================
-- Migration 021: Remove auto_renew fields from client_packs
-- =============================================================================
--
-- Esta migração é IDEMPOTENTE — segura de executar múltiplas vezes.
--
-- Alterações:
--   1. client_packs: remove campos auto_renew, next_pack_type_id, renewal_status
--      Razão: Renovação automática de packs nunca ocorre em produção.
--             A renovação é sempre uma conversa manual entre trainer e cliente.
--             Remover reduz complexidade e remove código não utilizado.
-- =============================================================================


-- Remover os 3 campos (se existirem)
ALTER TABLE client_packs
    DROP COLUMN IF EXISTS auto_renew,
    DROP COLUMN IF EXISTS next_pack_type_id,
    DROP COLUMN IF EXISTS renewal_status;
