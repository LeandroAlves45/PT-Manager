/**
 * Iniciais para o avatar sem fotografia: primeira letra do primeiro e do último nome.
 *
 * Ficheiro próprio, sem dependências: o avatar está no menu de perfil, que carrega em todas as
 * páginas, e importar `profile.ts` trazia o zod do formulário para esse carregamento.
 */
export function profileInitials(name: string): string {
  const parts = name.trim().split(/\s+/);
  const first = parts[0]?.[0] ?? '';
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
  return `${first}${last}`.toUpperCase();
}
