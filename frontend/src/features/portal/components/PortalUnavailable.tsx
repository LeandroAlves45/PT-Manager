import { UserX } from 'lucide-react';

import { EmptyState } from '@/shared/components/EmptyState';

/**
 * Ficha arquivada ou relação com o personal trainer terminada.
 *
 * Não é uma falha técnica: tentar de novo não ajudava, por isso não há botão. Cada ecrã
 * decide que código do backend o traz aqui, porque o código muda de endpoint para endpoint.
 */
export function PortalUnavailable() {
  return (
    <EmptyState
      titleAs="h1"
      icon={UserX}
      title="Portal indisponível"
      description="A tua ficha não está ativa. Fala com o teu personal trainer."
    />
  );
}
