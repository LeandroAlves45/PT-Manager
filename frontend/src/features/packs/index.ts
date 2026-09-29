/**
 * API pública da feature packs (tipos de pack e packs vendidos aos clientes).
 *
 * As sessões usam os packs com saldo e invalidam as listas depois de concluir/faltar; o
 * ecrã "Sessões e packs" e o detalhe do cliente montam os painéis.
 */
export { packKeys } from '@/features/packs/api/keys';
export { useUsablePacksQuery } from '@/features/packs/api/queries';
export { ClientPacksPanel } from '@/features/packs/components/ClientPacksPanel';
export { PackTypesPanel } from '@/features/packs/components/PackTypesPanel';
export { balanceLabel } from '@/features/packs/lib/labels';
