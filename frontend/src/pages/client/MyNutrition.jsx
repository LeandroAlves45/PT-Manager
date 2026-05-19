/**
 * MyNutrition.jsx — planos alimentares do cliente (read-only).
 *
 * Mostra os planos ativos com refeições, alimentos e macros totais.
 */

import { useEffect, useState } from 'react';
import { getMyMealPlans } from '@/api/clientPortalApi';
import { formatDate } from '@/utils/formatters';
import { UtensilsCrossed, ChevronDown, ChevronUp, Pill } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';

function MacroRow({ macros, label }) {
  if (!macros) return null;
  const { carbs_g = 0, protein_g = 0, fats_g = 0, kcal = 0 } = macros;
  return (
    <div className="flex flex-wrap gap-2 text-xs">
      {label && (
        <span className="text-muted-foreground font-medium w-full">
          {label}
        </span>
      )}
      <span className="px-2 py-0.5 rounded bg-orange-500/10 text-orange-600 dark:text-orange-400 font-medium">
        HC {Math.round(carbs_g)}g
      </span>
      <span className="px-2 py-0.5 rounded bg-blue-500/10 text-blue-600 dark:text-blue-400 font-medium">
        P {Math.round(protein_g)}g
      </span>
      <span className="px-2 py-0.5 rounded bg-yellow-500/10 text-yellow-600 dark:text-yellow-400 font-medium">
        G {Math.round(fats_g)}g
      </span>
      <span className="px-2 py-0.5 rounded bg-primary/10 text-primary font-semibold">
        {Math.round(kcal)} kcal
      </span>
    </div>
  );
}

export default function MyNutrition() {
  const [plans, setPlans] = useState([]);
  const [loading, setLoading] = useState(true);

  const [expandedMeals, setExpandedMeals] = useState(new Set());

  useEffect(() => {
    getMyMealPlans()
      .then((data) => {
        setPlans(data);
        // Expande automaticamente a primeira refeição de cada plano ao carregar
        const initialExpanded = new Set(
          data.flatMap((plan) =>
            plan.meals?.length > 0 ? [plan.meals[0].id] : []
          )
        );
        setExpandedMeals(initialExpanded);
      })
      .finally(() => setLoading(false));
  }, []);

  function toggleMeal(mealId) {
    setExpandedMeals((prev) => {
      const next = new Set(prev);
      next.has(mealId) ? next.delete(mealId) : next.add(mealId);
      return next;
    });
  }

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-64">
        <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
      </div>
    );
  }

  if (plans.length === 0) {
    return (
      <div className="p-4 lg:p-6">
        <h1 className="text-2xl font-semibold text-foreground mb-2">
          Nutrição
        </h1>
        <div className="rounded-lg border border-border bg-card p-8 flex flex-col items-center gap-3 text-center">
          <UtensilsCrossed className="h-10 w-10 text-muted-foreground" />
          <p className="font-medium text-muted-foreground">
            Sem planos alimentares
          </p>
          <p className="text-sm text-muted-foreground">
            O seu treinador ainda não lhe atribuiu um plano alimentar.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* Cabeçalho */}
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Nutrição</h1>
        <p className="text-sm text-muted-foreground mt-1">
          {plans.length} plano{plans.length !== 1 ? 's' : ''} alimentar
          {plans.length !== 1 ? 'es' : ''}
        </p>
      </div>

      {plans.map((plan) => (
        <Card key={plan.id} className="border-border bg-card">
          <CardHeader className="pb-3">
            {/* Nome + badges de tipo e estado */}
            <div className="flex items-start justify-between gap-2 flex-wrap">
              <CardTitle className="text-base">{plan.name}</CardTitle>
              <div className="flex gap-2 flex-wrap">
                {/* plan_type_label é o label legível devolvido pelo backend */}
                {plan.plan_type_label && (
                  <Badge variant="secondary" className="text-xs">
                    {plan.plan_type_label}
                  </Badge>
                )}
                {plan.active && (
                  <Badge className="text-xs bg-green-500/10 text-green-600 dark:text-green-400 border-0">
                    Ativo
                  </Badge>
                )}
              </div>
            </div>

            {/* Datas de vigência (se definidas) */}
            {(plan.starts_date || plan.ends_date) && (
              <p className="text-xs text-muted-foreground mt-1">
                {plan.starts_date && formatDate(plan.starts_date)}
                {plan.starts_date && plan.ends_date && ' → '}
                {plan.ends_date && formatDate(plan.ends_date)}
              </p>
            )}

            {/* Notas do Personal Trainer */}
            {plan.notes && (
              <p className="text-sm text-muted-foreground italic mt-1">
                {plan.notes}
              </p>
            )}

            {/* Targets de macros definidos pelo Personal Trainer (se existirem) */}
            {(plan.kcal_target ||
              plan.protein_target_g ||
              plan.carbs_target_g ||
              plan.fats_target_g) && (
              <div className="mt-2">
                <p className="text-xs text-muted-foreground mb-1 uppercase tracking-wide font-medium">
                  Objectivos
                </p>
                <div className="flex flex-wrap gap-2 text-xs">
                  {plan.kcal_target && (
                    <span className="px-2 py-0.5 rounded bg-primary/10 text-primary font-medium">
                      {Math.round(plan.kcal_target)} kcal
                    </span>
                  )}
                  {plan.carbs_target_g && (
                    <span className="px-2 py-0.5 rounded bg-blue-500/10 text-blue-600 dark:text-blue-400">
                      HC {Math.round(plan.carbs_target_g)}g
                    </span>
                  )}
                  {plan.protein_target_g && (
                    <span className="px-2 py-0.5 rounded bg-green-500/10 text-green-600 dark:text-green-400">
                      P {Math.round(plan.protein_target_g)}g
                    </span>
                  )}
                  {plan.fats_target_g && (
                    <span className="px-2 py-0.5 rounded bg-orange-500/10 text-orange-600 dark:text-orange-400">
                      G {Math.round(plan.fats_target_g)}g
                    </span>
                  )}
                </div>
              </div>
            )}
          </CardHeader>

          <CardContent className="flex flex-col gap-3 pt-0">
            {/* Lista de refeições */}
            {plan.meals?.map((meal, mealIdx) => {
              const isExpanded = expandedMeals.has(meal.id);
              return (
                <div
                  key={meal.id}
                  className="border border-border rounded-lg overflow-hidden"
                >
                  {/* Cabeçalho da refeição — clicável para expandir/colapsar */}
                  <button
                    type="button"
                    onClick={() => toggleMeal(meal.id)}
                    className="w-full flex items-center justify-between px-4 py-3 bg-muted/30 hover:bg-muted/50 transition-colors text-left"
                  >
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium text-foreground">
                        {meal.name}
                      </span>
                      <span className="text-xs text-muted-foreground">
                        ({meal.items?.length ?? 0} alimento
                        {meal.items?.length !== 1 ? 's' : ''})
                      </span>
                    </div>
                    <div className="flex items-center gap-3">
                      {/* Total kcal da refeição no cabeçalho — sempre visível */}
                      {meal.meal_macros && (
                        <span className="text-xs font-semibold text-primary">
                          {Math.round(meal.meal_macros.kcal)} kcal
                        </span>
                      )}
                      {isExpanded ? (
                        <ChevronUp className="h-4 w-4 text-muted-foreground shrink-0" />
                      ) : (
                        <ChevronDown className="h-4 w-4 text-muted-foreground shrink-0" />
                      )}
                    </div>
                  </button>

                  {/* Conteúdo expandido: lista de alimentos + macros da refeição */}
                  {isExpanded && (
                    <div className="px-4 py-3 space-y-3">
                      {/* Alimentos */}
                      {meal.items?.length > 0 ? (
                        <div className="space-y-1.5">
                          {meal.items.map((item) => (
                            <div
                              key={item.id}
                              className="flex items-center text-sm py-1.5 border-b border-border/50 last:border-0"
                            >
                              <span className="text-foreground">
                                {item.food_name}
                                {item.quantity_grams && (
                                  <span className="text-muted-foreground ml-1.5 text-xs">
                                    {item.quantity_grams}g
                                  </span>
                                )}
                              </span>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <p className="text-xs text-muted-foreground">
                          Sem alimentos nesta refeição.
                        </p>
                      )}

                      {/* Totais da refeição */}
                      {meal.meal_macros && (
                        <MacroRow
                          macros={meal.meal_macros}
                          label="Total da refeição"
                        />
                      )}
                      {/* NR-04: suplementos associados a esta refeição */}
                      {meal.supplements && meal.supplements.length > 0 && (
                        <div className="pt-2 border-t border-border space-y-1.5">
                          <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
                            Suplementos desta refeição
                          </p>
                          <div className="flex flex-wrap gap-1.5">
                            {meal.supplements.map((s) => (
                              <div
                                key={s.id}
                                className="flex items-center gap-1 px-2.5 py-1 rounded-full bg-primary/8 border border-primary/20 text-xs"
                              >
                                <Pill className="h-3 w-3 text-primary shrink-0" />
                                <span className="text-foreground font-medium">
                                  {s.supplement_name}
                                </span>
                                {s.supplement_timing && (
                                  <span className="text-muted-foreground">
                                    · {s.supplement_timing}
                                  </span>
                                )}
                                {s.notes && (
                                  <span className="text-muted-foreground italic">
                                    — {s.notes}
                                  </span>
                                )}
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
