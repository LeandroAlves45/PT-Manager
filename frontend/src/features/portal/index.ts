/**
 * API pública da feature portal.
 *
 * O `PortalLayout` (camada `app`) usa a marca e o menu de perfil usa o perfil e a fotografia do
 * cliente. As páginas não saem por aqui: o router importa-as uma a uma (rotas lazy), para o
 * cabeçalho do portal não arrastar o código de todas as páginas.
 */
export {
  portalKeys,
  usePortalBrandingQuery,
  usePortalProfileQuery,
} from '@/features/portal/api/portal';
export { BrandMark } from '@/shared/components/BrandMark';
export { ClientAvatar } from '@/features/portal/components/ClientAvatar';
