import type { LibraryKind } from '@/features/library/api/keys';

/** Texto de cada separador; todos os substantivos são masculinos, o que simplifica o copy. */
export const LIBRARY_TEXTS: Readonly<
  Record<
    LibraryKind,
    {
      readonly tab: string;
      readonly singular: string;
      readonly plural: string;
      readonly newTitle: string;
      readonly editTitle: string;
      readonly createLabel: string;
      readonly emptyTitle: string;
      readonly emptyDescription: string;
      readonly searchMaxLength: number;
    }
  >
> = {
  exercises: {
    tab: 'Exercícios',
    singular: 'exercício',
    plural: 'exercícios',
    newTitle: 'Novo exercício',
    editTitle: 'Editar exercício',
    createLabel: 'Criar exercício',
    emptyTitle: 'Ainda não há exercícios',
    emptyDescription: 'Cria o primeiro exercício privado para o usares nos planos de treino.',
    searchMaxLength: 200,
  },
  foods: {
    tab: 'Alimentos',
    singular: 'alimento',
    plural: 'alimentos',
    newTitle: 'Novo alimento',
    editTitle: 'Editar alimento',
    createLabel: 'Criar alimento',
    emptyTitle: 'Ainda não há alimentos',
    emptyDescription: 'Cria o primeiro alimento privado para o usares nos planos alimentares.',
    searchMaxLength: 255,
  },
  supplements: {
    tab: 'Suplementos',
    singular: 'suplemento',
    plural: 'suplementos',
    newTitle: 'Novo suplemento',
    editTitle: 'Editar suplemento',
    createLabel: 'Criar suplemento',
    emptyTitle: 'Ainda não há suplementos',
    emptyDescription: 'Cria o primeiro suplemento privado para o atribuíres aos clientes.',
    searchMaxLength: 255,
  },
};

export const ACTIVITY_OPTIONS = ['active', 'archived', 'all'] as const;
export const ACTIVITY_LABELS = { active: 'Ativos', archived: 'Arquivados', all: 'Todos' } as const;

/** Número com até duas casas, á portuguesa: 31 -> "31", 2.5 -> "2,5". */
export function gramsLabel(value: number): string {
  return value.toLocaleString('pt-PT', { maximumFractionDigits: 2 });
}
