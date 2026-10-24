import { ClipboardList } from 'lucide-react';

import { PortalCard } from '@/features/portal/components/PortalCard';

/** Caminho do preenchimento da avaliação inicial. */
export const INITIAL_ASSESSMENT_PATH = '/portal/initial-assessment';

/**
 * Cartão do Início enquanto a avaliação inicial não foi entregue. O cliente continua a poder
 * usar o resto do portal; o cartão sai quando a avaliação fica guardada.
 */
export function InitialAssessmentCard() {
  return (
    <PortalCard
      title="Avaliação inicial"
      icon={ClipboardList}
      tone="assessment"
      action={{ label: 'Preencher avaliação inicial', to: INITIAL_ASSESSMENT_PATH }}
    >
      <p className="font-display pe-1 text-xl leading-tight">Falta a tua avaliação inicial</p>
      <p className="text-muted-foreground text-sm">
        O teu personal trainer precisa dela para preparar os teus planos. Só a preenches uma vez.
      </p>
    </PortalCard>
  );
}
