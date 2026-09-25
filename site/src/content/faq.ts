import type { FaqItem } from '@/domain/types';

/** Perguntas frequentes. Respostas verificadas contra o backend (2026-09-25). */
export const faq: readonly FaqItem[] = [
  {
    id: 'free',
    question: 'O plano Free tem prazo?',
    answer:
      'Não. Podes usar o plano Free com até 5 clientes durante o tempo que quiseres. Quando precisares de mais clientes, mudas para Starter ou Pro.',
  },
  {
    id: 'card',
    question: 'Preciso de cartão de crédito para começar?',
    answer: 'Não. Crias a conta e começas no plano Free sem introduzir dados de pagamento.',
  },
  {
    id: 'branding',
    question: 'Como funciona a marca própria no portal do cliente?',
    answer:
      'Carregas o teu logo e escolhes as cores nas definições. O portal que os teus clientes usam passa a mostrar a tua marca, em qualquer plano, incluindo o Free.',
  },
  {
    id: 'billing',
    question: 'Como pago a subscrição?',
    answer:
      'Os planos pagos são cobrados por cartão através da Stripe. Podes gerir a subscrição no portal de faturação, a partir da tua conta.',
  },
  {
    id: 'limit',
    question: 'O que acontece se atingir o limite de clientes do plano?',
    answer:
      'Os clientes que já tens continuam a funcionar normalmente. Para adicionar novos, podes arquivar clientes inativos ou mudar para um plano com mais capacidade.',
  },
];
