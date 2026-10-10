/**
 * API pública da feature portal.
 *
 * O `PortalLayout` (camada `app`) usa a marca e o router usa as páginas (Início, treino de hoje
 * e plano); as fases seguintes do portal invalidam `portalKeys.home()` depois de escrever.
 */
export { portalKeys, usePortalBrandingQuery } from '@/features/portal/api/portal';
export { BrandMark } from '@/features/portal/components/BrandMark';
export { PortalHomePage } from '@/features/portal/pages/PortalHomePage';
export { PortalPlanPage } from '@/features/portal/pages/PortalPlanPage';
export { PortalTodayPage } from '@/features/portal/pages/PortalTodayPage';
