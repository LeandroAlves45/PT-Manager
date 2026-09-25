import type { Feature } from '@/domain/types';

/**
 * Funcionalidades, pela ordem da grelha bento.
 *
 * Cada frase foi verificada contra o backend (2026-09-25). Não prometer o que o código não
 * faz: o Stripe só cobra a subscrição do trainer ao PT Manager; não há cobrança aos
 * clientes do trainer, por isso não há cartão "pagamentos automáticos".
 */
export const features: readonly Feature[] = [
  {
    id: 'nutrition',
    icon: 'nutrition',
    title: 'Planos de nutrição',
    description: 'Planos alimentares com calorias e macros calculados automaticamente.',
    span: 'third',
  },
  {
    id: 'checkins',
    icon: 'checkins',
    title: 'Check-ins e progresso',
    description:
      'Recebe check-ins com peso, medidas e adesão, e acompanha a evolução de cada cliente.',
    span: 'third',
  },
  {
    id: 'training',
    icon: 'training',
    title: 'Planos de treino e biblioteca de exercícios',
    description: 'Biblioteca global e privada, com vídeo por exercício.',
    span: 'half',
    tags: [{ label: 'Global' }, { label: 'Privada' }, { label: 'Vídeo', highlight: true }],
  },
  {
    id: 'clients',
    icon: 'clients',
    title: 'Gestão de clientes',
    description: 'Cria, arquiva e reativa clientes, com convite por email para o portal.',
    span: 'half',
    tags: [{ label: 'Convite' }, { label: 'Arquivar' }, { label: 'Reativar' }],
  },
  {
    id: 'supplements',
    icon: 'supplements',
    title: 'Planos de suplementação',
    description: 'Suplementação organizada por cliente, junto ao treino e à nutrição.',
    span: 'third',
  },
  {
    id: 'assessments',
    icon: 'assessments',
    title: 'Avaliação inicial',
    description: 'Regista o ponto de partida de cada cliente antes de prescrever.',
    span: 'third',
  },
  {
    id: 'sessions',
    icon: 'sessions',
    title: 'Sessões e packs',
    description: 'Regista sessões e controla os packs de sessões de cada cliente.',
    span: 'third',
  },
];
