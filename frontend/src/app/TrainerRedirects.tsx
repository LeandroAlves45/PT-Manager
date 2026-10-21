import { Navigate, useParams } from 'react-router';

/**
 * Redirecionamentos de segmentos intermédios da área do personal trainer.
 *
 * As migalhas ligam cada segmento do caminho. Estes segmentos não são páginas por si: levam à
 * página que os mostra, para nenhuma migalha acabar na 404.
 */

const LIBRARY_KINDS = ['exercises', 'foods', 'supplements'] as const;

/** `/trainer/library/:kind` → separador desse tipo na biblioteca. */
export function LibraryKindRedirect() {
  const { kind } = useParams();
  const tab = LIBRARY_KINDS.find((item) => item === kind);
  return (
    <Navigate
      replace
      to={
        tab === undefined || tab === 'exercises'
          ? '/trainer/library'
          : `/trainer/library?tab=${tab}`
      }
    />
  );
}

/** `/trainer/sessions/pack-types` → separador «Tipos de pack». */
export function PackTypesRedirect() {
  return <Navigate replace to="/trainer/sessions?tab=types" />;
}

/** `/trainer/training-plans/:planId/set-logs` → página do plano, onde estão as séries. */
export function SetLogsRedirect() {
  const { planId } = useParams();
  return <Navigate replace to={`/trainer/training-plans/${planId ?? ''}`} />;
}
