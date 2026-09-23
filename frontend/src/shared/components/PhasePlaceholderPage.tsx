import { Construction } from 'lucide-react';

import { EmptyState } from '@/shared/components/EmptyState';
import { PageHeader } from '@/shared/components/PageHeader';

/**
 * Página de espaço reservado para as rotas.
 *
 * Existe por uma razão concreta: sem uma rota real por item de navegação não é possível
 * provar os guards, as migalhas nem o estado activo da sidebar. Cada fase seguinte
 * substitui estes ecrãs pelos verdadeiros — não é UI descartável, é a moldura a ser
 * testada desde o primeiro dia.
 */
export function PhasePlaceholderPage({ title, phase }: { title: string; phase: string }) {
  return (
    <>
      <PageHeader title={title} description={`Este ecrã é construído na fase ${phase}.`} />
      <EmptyState
        icon={Construction}
        title="Ainda não há nada aqui."
        description={`A navegação, os guards e a moldura já funcionam. O conteúdo chega na fase ${phase}.`}
      />
    </>
  );
}
