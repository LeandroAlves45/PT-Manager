import { ClipboardList } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link } from 'react-router';

import { usePortalInitialAssessmentStatusQuery } from '@/features/portal/api/portal';
import { INITIAL_ASSESSMENT_PATH } from '@/features/portal/components/InitialAssessmentCard';

/**
 * Aviso de que falta a avaliação inicial, para as páginas de treino, nutrição, suplementos e
 * avaliações. Não bloqueia a página: enquanto o estado carrega, ou se falhar, não mostra nada.
 */
export function InitialAssessmentMissingNotice() {
  const status = usePortalInitialAssessmentStatusQuery();
  if (status.data?.pending !== true) return null;

  return (
    <div
      role="status"
      className="border-border bg-card border-l-section-assessment flex items-start gap-3 rounded-lg border border-l-4 p-3 text-sm"
    >
      <ClipboardList aria-hidden className="text-section-assessment mt-0.5 size-5 shrink-0" />
      <div className="space-y-1">
        <p>
          Falta a tua avaliação inicial. O teu personal trainer só prepara o planeamento depois de a
          preencheres.
        </p>
        <Link
          to={INITIAL_ASSESSMENT_PATH}
          className="text-primary inline-block py-2 font-medium underline-offset-2 hover:underline"
        >
          Preencher avaliação inicial
        </Link>
      </div>
    </div>
  );
}

/**
 * Páginas cujo conteúdo não precisa de mudar para mostrar o aviso: o aviso fica acima, com o
 * mesmo espaçamento dos blocos da página.
 */
export function WithInitialAssessmentNotice({ children }: { children: ReactNode }) {
  return (
    <div className="space-y-4">
      <InitialAssessmentMissingNotice />
      {children}
    </div>
  );
}
