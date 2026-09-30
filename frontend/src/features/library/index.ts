/**
 * API pública da feature library (biblioteca do personal trainer: exercícios, alimentos e
 * suplementos, globais e privados).
 *
 * O router monta `pages/LibraryPage` diretamente (como as outras páginas). A 6E-4 (editores de
 * planos) reutiliza as query keys nas suas pesquisas de catálogo, para uma escrita na
 * biblioteca as refrescar.
 */
export { libraryKeys, type LibraryKind } from '@/features/library/api/keys';
