import { Link } from 'react-router';

import { mealSupplementDose } from '@/features/portal/lib/nutrition';
import type { components } from '@/shared/api/schema';
import { Badge } from '@/shared/components/ui/badge';

type MealSupplement = components['schemas']['MyMealSupplementResponse'];

/** Rota e âncora da secção do plano alimentar na página de suplementos. */
export const PLAN_SUPPLEMENTS_LINK = '/portal/supplements#plano-alimentar';

/**
 * Suplementos de uma refeição ou de fora das refeições, para ler, com o link para registar as
 * tomas na secção do plano da página de suplementos.
 *
 * Um suplemento cujo item do catálogo deixou de estar ativo aparece como «Indisponível» e
 * explica o que fazer.
 */
export function MealSupplementList({
  supplements,
  title,
}: {
  supplements: readonly MealSupplement[];
  title: string;
}) {
  if (supplements.length === 0) return null;

  return (
    <div className="space-y-1 pt-2">
      <h3 className="text-sm font-medium">{title}</h3>
      <ul className="space-y-1 text-sm">
        {supplements.map((supplement) => (
          <li key={supplement.id} className="space-y-0.5">
            <p className="flex flex-wrap items-center gap-2">
              <span className="min-w-0 wrap-break-word">
                {supplement.supplement_name} · {mealSupplementDose(supplement)}
              </span>
              {supplement.is_unavailable && <Badge variant="secondary">Indisponível</Badge>}
            </p>
            {supplement.is_unavailable && (
              <p className="text-muted-foreground">
                Este suplemento já não está disponível. Confirma com o teu personal trainer o que
                fazer.
              </p>
            )}
            {supplement.notes !== null && (
              <p className="text-muted-foreground">{supplement.notes}</p>
            )}
          </li>
        ))}
      </ul>
      <Link
        to={PLAN_SUPPLEMENTS_LINK}
        className="text-foreground inline-flex min-h-11 items-center text-sm underline underline-offset-4"
      >
        Registar tomas
      </Link>
    </div>
  );
}
