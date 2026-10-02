import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { toast } from 'sonner';

import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { MealStructureEditor } from '@/features/prescriptions/components/MealStructureEditor';
import { mealStructureFromDetails } from '@/features/prescriptions/lib/meal-structure';
import { NutritionCalculationFields } from '@/features/prescriptions/components/NutritionCalculationFields';
import { prescriptionError } from '@/features/prescriptions/lib/errors';
import {
  EMPTY_CALCULATION,
  calculationFromResult,
  normalizeCalculation,
  type Calculation,
  type CalculationResult,
} from '@/features/prescriptions/lib/nutrition';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';

type Structure = components['schemas']['MealPlanStructureRequest'];

function ageOnDate(birthDate: string, today: Date): number {
  const [year = 0, month = 1, day = 1] = birthDate.split('-').map(Number);
  let age = today.getFullYear() - year;

  if (today.getMonth() + 1 < month || (today.getMonth() + 1 === month && today.getDate() < day))
    age--;
  return age;
}

/**
 * Editor de um plano alimentar novo (`planId === null`) ou existente.
 *
 * Cálculo: num plano novo, a ficha e a avaliação inicial do cliente escolhido sugerem
 * peso, altura, sexo, idade e atividade, sem pisar valores já editados. Qualquer mudança
 * no cálculo anula o preview e gravar exige novo `Calcular`. Respostas de preview de uma
 * revisão antiga (sucesso ou erro) são ignoradas. Um `PUT` sem mudança no cálculo envia
 * `calculation: null`, e o servidor mantém o snapshot.
 *
 * Estrutura: o `PUT` reconcilia refeições, itens e suplementos por ID. O rascunho
 * inicializa uma vez por plano: refetches não apagam edições nem o preview.
 *
 * @param planId Plano a editar, ou `null` para criar.
 * @param fixedClient Cliente da ficha; `null` mostra o seletor de cliente num plano novo.
 * @param onClose Volta à lista; chamado também depois de gravar.
 */
export function MealPlanEditor({
  planId,
  fixedClient,
  onClose,
}: {
  planId: string | null;
  fixedClient: ClientChoice | null;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const [client, setClient] = useState<ClientChoice | null>(fixedClient);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [startsDate, setStartsDate] = useState('');
  const [endsDate, setEndsDate] = useState('');
  const [calculation, setCalculation] = useState<Calculation>(EMPTY_CALCULATION);
  const [calculationDirty, setCalculationDirty] = useState(false);
  const [preview, setPreview] = useState<CalculationResult | null>(null);
  const [structure, setStructure] = useState<Structure>({ meals: [] });
  const [names, setNames] = useState<Record<string, string>>({});
  const [error, setError] = useState('');
  const hasEdited = useRef(false);
  const calculationRevision = useRef(0);
  const prefilledClientId = useRef<string | null>(null);
  const initializedFor = useRef<string | null>(null);

  const detail = useQuery({
    queryKey: prescriptionKeys.mealsDetail(planId ?? ''),
    enabled: planId !== null,
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/meal-plans/{mealPlanId}', {
          params: { path: { mealPlanId: planId ?? '' } },
          signal,
        })
        .then(unwrap),
  });
  const effectiveClientId = client?.id ?? detail.data?.client_id ?? '';
  const clientDetails = useQuery({
    queryKey: prescriptionKeys.nutritionClient(effectiveClientId),
    enabled: planId === null && effectiveClientId !== '',
    queryFn: ({ signal }) =>
      apiClient
        .GET('/api/v1/clients/{clientId}', {
          params: { path: { clientId: effectiveClientId } },
          signal,
        })
        .then(unwrap),
  });
  const assessment = useQuery({
    queryKey: prescriptionKeys.nutritionAssessment(effectiveClientId),
    enabled: planId === null && clientDetails.isSuccess,
    queryFn: async ({ signal }) => {
      try {
        return unwrap(
          await apiClient.GET('/api/v1/clients/{clientId}/initial-assessment', {
            params: { path: { clientId: effectiveClientId } },
            signal,
          })
        );
      } catch (failure) {
        if (isApiProblem(failure) && failure.status === 404) return null;
        throw failure;
      }
    },
  });

  useEffect(() => {
    // Inicializa o rascunho uma única vez por plano: um refetch (foco da janela)
    // não pode apagar edições nem o preview por gravar.
    if (detail.data === undefined || initializedFor.current === detail.data.id) return;
    const plan = detail.data;
    initializedFor.current = plan.id;
    setClient({ id: plan.client_id, name: 'Cliente' });
    setName(plan.name);
    setDescription(plan.description ?? '');
    setStartsDate(plan.starts_date);
    setEndsDate(plan.ends_date ?? '');
    setCalculation(calculationFromResult(plan.calculation));
    setPreview(plan.calculation);
    setCalculationDirty(false);
    setStructure(mealStructureFromDetails(plan));
    setNames(
      Object.fromEntries(
        plan.meals.flatMap((meal) => [
          ...meal.items.map((item) => [item.food_id, item.food_name]),
          ...meal.supplements.map((item) => [item.supplement_id, item.supplement_name]),
        ])
      ) as Record<string, string>
    );
  }, [detail.data]);

  useEffect(() => {
    if (
      planId !== null ||
      clientDetails.data === undefined ||
      prefilledClientId.current === effectiveClientId ||
      hasEdited.current
    )
      return;

    if (!assessment.isSuccess) return;
    prefilledClientId.current = effectiveClientId;
    const candidate = clientDetails.data;
    const measurement = assessment.data;
    // Sugestões chegam de forma assíncrona e não devem substituir um rascunho editado.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setCalculation((current) => ({
      ...current,
      weight_kg: measurement?.weight_kg ?? current.weight_kg,
      height_cm: measurement?.height_cm ?? current.height_cm,
      body_fat_percentage: measurement?.body_fat_percentage ?? current.body_fat_percentage,
      activity_level: measurement?.activity_level ?? current.activity_level,
      sex: candidate.sex,
      age: ageOnDate(candidate.birth_date, new Date()),
    }));
  }, [planId, clientDetails.data, assessment.isSuccess, assessment.data, effectiveClientId]);

  const readOnly = detail.data?.is_archived === true;
  const needsPreview = planId === null || calculationDirty;
  const previewMutation = useMutation({
    mutationFn: async (input: { revision: number; calculation: Calculation }) =>
      unwrap(
        await apiClient.POST('/api/v1/nutrition/preview', {
          body: { calculation: normalizeCalculation(input.calculation) },
        })
      ),
    onSuccess: (result, input) => {
      if (input.revision !== calculationRevision.current) return;
      setPreview(result);
      setError('');
    },
    onError: (failure, input) => {
      // Um erro de um cálculo já alterado não pode aparecer sobre o cálculo novo.
      if (input.revision !== calculationRevision.current) return;
      setError(prescriptionError(failure));
    },
  });

  const save = useMutation({
    mutationFn: async () => {
      const metadata = {
        name: name.trim(),
        description: description.trim() || null,
        starts_date: startsDate,
        ends_date: endsDate || null,
        structure,
      };
      if (planId === null) {
        if (client === null) throw new Error('client_required');
        return unwrap(
          await apiClient.POST('/api/v1/meal-plans', {
            body: {
              client_id: client.id,
              ...metadata,
              calculation: normalizeCalculation(calculation),
            },
          })
        );
      }
      return unwrap(
        await apiClient.PUT('/api/v1/meal-plans/{mealPlanId}', {
          params: { path: { mealPlanId: planId } },
          body: {
            ...metadata,
            calculation: calculationDirty ? normalizeCalculation(calculation) : null,
          },
        })
      );
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.meals });
      toast.success('Plano alimentar guardado.');
      onClose();
    },
    onError: (failure) => setError(prescriptionError(failure)),
  });

  if (detail.isPending && planId !== null) {
    return (
      <Skeleton role="status" aria-label="A carregar plano alimentar…" className="h-80 w-full" />
    );
  }
  if (detail.isError && planId !== null) {
    return <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="font-display text-2xl">{planId === null ? 'Novo plano alimentar' : name}</h2>
        <Button variant="outline" onClick={onClose}>
          Fechar
        </Button>
      </div>
      {readOnly && <p role="status">Plano arquivado. Só consulta.</p>}
      {error !== '' && (
        <p role="alert" className="text-destructive">
          {error}
        </p>
      )}
      <form
        className="space-y-4"
        onSubmit={(event) => {
          event.preventDefault();
          setError('');
          if ((planId === null && client === null) || name.trim() === '' || startsDate === '') {
            setError('Escolhe o cliente, o nome e a data de início.');
            return;
          }
          if (needsPreview && preview === null) {
            setError('Calcula o preview antes de guardar.');
            return;
          }
          void save.mutateAsync().catch(() => undefined);
        }}
      >
        {planId === null && fixedClient === null && (
          <FormField label="Cliente">
            {(control) => (
              <ClientCombobox
                {...control}
                value={client}
                onChange={(choice) => {
                  setClient(choice);
                  prefilledClientId.current = null;
                  hasEdited.current = false;
                  setCalculation(EMPTY_CALCULATION);
                  calculationRevision.current++;
                  setPreview(null);
                }}
              />
            )}
          </FormField>
        )}
        <FormField label="Nome">
          {(control) => (
            <Input
              {...control}
              value={name}
              maxLength={255}
              required
              disabled={readOnly}
              onChange={(event) => setName(event.target.value)}
            />
          )}
        </FormField>
        <FormField label="Descrição">
          {(control) => (
            <Input
              {...control}
              value={description}
              disabled={readOnly}
              onChange={(event) => setDescription(event.target.value)}
            />
          )}
        </FormField>
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Data de início">
            {(control) => (
              <Input
                {...control}
                type="date"
                value={startsDate}
                required
                disabled={readOnly}
                onChange={(event) => setStartsDate(event.target.value)}
              />
            )}
          </FormField>
          <FormField label="Data de fim">
            {(control) => (
              <Input
                {...control}
                type="date"
                value={endsDate}
                disabled={readOnly}
                onChange={(event) => setEndsDate(event.target.value)}
              />
            )}
          </FormField>
        </div>
        {/* isLoading e não isPending: uma query desativada (plano existente) fica pending para sempre. */}
        {(clientDetails.isLoading || assessment.isLoading) && (
          <p role="status">A carregar dados do cliente…</p>
        )}
        {clientDetails.isError && (
          <ErrorState error={clientDetails.error} onRetry={() => void clientDetails.refetch()} />
        )}
        {assessment.isError && (
          <ErrorState error={assessment.error} onRetry={() => void assessment.refetch()} />
        )}
        <NutritionCalculationFields
          value={calculation}
          disabled={readOnly}
          onChange={(next) => {
            hasEdited.current = true;
            setCalculation(next);
            calculationRevision.current++;
            setCalculationDirty(true);
            setPreview(null);
          }}
        />
        {!readOnly && (
          <Button
            type="button"
            variant="outline"
            disabled={previewMutation.isPending}
            onClick={() =>
              void previewMutation
                .mutateAsync({
                  revision: calculationRevision.current,
                  calculation,
                })
                .catch(() => undefined)
            }
          >
            {previewMutation.isPending ? 'A calcular…' : 'Calcular'}
          </Button>
        )}
        {needsPreview && preview === null && <p role="status">Preview por calcular.</p>}
        {preview !== null && (
          <div role="status" className="border-border rounded-xl border p-4">
            <p>Energia alvo: {preview.target_kcal} kcal</p>
            <p>
              Proteína: {preview.protein_target_grams} g · Hidratos: {preview.carbs_target_grams} g
              · Gordura: {preview.fats_target_grams} g
            </p>
          </div>
        )}
        <MealStructureEditor
          value={structure}
          onChange={setStructure}
          disabled={readOnly}
          initialNames={names}
        />
        {!readOnly && (
          <Button type="submit" disabled={save.isPending || (needsPreview && preview === null)}>
            {save.isPending ? 'A guardar…' : 'Guardar plano alimentar'}
          </Button>
        )}
      </form>
    </div>
  );
}
