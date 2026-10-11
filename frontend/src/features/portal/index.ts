/**
 * API pública da feature portal.
 *
 * O `PortalLayout` (camada `app`) usa a marca e o router usa as páginas (Início, treino de hoje,
 * plano, nutrição e suplementos); as fases seguintes do portal invalidam `portalKeys.home()`
 * depois de escrever.
 */
export { portalKeys, usePortalBrandingQuery } from '@/features/portal/api/portal';
export { BrandMark } from '@/features/portal/components/BrandMark';
export { PortalHomePage } from '@/features/portal/pages/PortalHomePage';
export { PortalNutritionPage } from '@/features/portal/pages/PortalNutritionPage';
export { PortalPlanPage } from '@/features/portal/pages/PortalPlanPage';
export { PortalSupplementsPage } from '@/features/portal/pages/PortalSupplementsPage';
export { PortalTodayPage } from '@/features/portal/pages/PortalTodayPage';
