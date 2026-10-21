/**
 * API pública da feature portal.
 *
 * O `PortalLayout` (camada `app`) usa a marca, o menu de perfil usa o perfil e a fotografia do
 * cliente, e o router usa as páginas (Início, treino de hoje, plano, nutrição, suplementos,
 * check-ins e perfil).
 */
export {
  portalKeys,
  usePortalBrandingQuery,
  usePortalProfileQuery,
} from '@/features/portal/api/portal';
export { BrandMark } from '@/shared/components/BrandMark';
export { ClientAvatar } from '@/features/portal/components/ClientAvatar';
export { PortalCheckInsPage } from '@/features/portal/pages/PortalCheckInsPage';
export { PortalHomePage } from '@/features/portal/pages/PortalHomePage';
export { PortalNutritionPage } from '@/features/portal/pages/PortalNutritionPage';
export { PortalPlanPage } from '@/features/portal/pages/PortalPlanPage';
export { PortalProfilePage } from '@/features/portal/pages/PortalProfilePage';
export { PortalSupplementsPage } from '@/features/portal/pages/PortalSupplementsPage';
export { PortalTodayPage } from '@/features/portal/pages/PortalTodayPage';
