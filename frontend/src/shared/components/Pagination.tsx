import { Button } from '@/shared/components/ui/button';

/**
 * "Anterior · Página X de Y · Seguinte" das tabelas paginadas no servidor.
 *
 * Não aparece quando tudo cabe numa página. A página atual vem de quem chama (URL ou
 * estado local); este componente só pede a mudança.
 *
 * @param label Nome acessível da navegação (ex.: "Páginas de sessões").
 * @param total `total_count` da resposta paginada.
 */
export function Pagination({
  label,
  page,
  total,
  pageSize,
  onPageChange,
}: {
  label: string;
  page: number;
  total: number;
  pageSize: number;
  onPageChange: (page: number) => void;
}) {
  if (total <= pageSize) return null;

  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  return (
    <nav aria-label={label} className="flex items-center justify-end gap-3">
      <Button variant="outline" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
        Anterior
      </Button>
      <span>
        Página {page} de {pageCount}
      </span>
      <Button variant="outline" disabled={page >= pageCount} onClick={() => onPageChange(page + 1)}>
        Seguinte
      </Button>
    </nav>
  );
}
