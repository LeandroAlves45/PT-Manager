import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, UtensilsCrossed } from 'lucide-react';
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  Link,
  Navigate,
  Outlet,
  useBlocker,
  useLocation,
  useNavigate,
  useParams,
  useSearchParams,
} from 'react-router';
import { toast } from 'sonner';

import {
  useFoodNutrients,
  useMealPlanDetailQuery,
  useNutritionAssessmentQuery,
  useNutritionClientQuery,
  useSupplementUnits,
} from '@/features/prescriptions/api/meal-plans';
import { prescriptionKeys } from '@/features/prescriptions/api/keys';
import { ClientCombobox, type ClientChoice } from '@/features/clients';
import { MealStructureEditor } from '@/features/prescriptions/components/MealStructureEditor';
import {
  NutritionBalance,
  TargetsSummary,
} from '@/features/prescriptions/components/NutritionBalance';
import { NutritionCalculationFields } from '@/features/prescriptions/components/NutritionCalculationFields';
import {
  blockedItemsFromDetails,
  NO_BLOCKED_ITEMS,
  sumUnblockedFoods,
} from '@/features/prescriptions/lib/blocked-items';
import { prescriptionError } from '@/features/prescriptions/lib/errors';
import { mealStructureFromDetails } from '@/features/prescriptions/lib/meal-structure';
import {
  mealPlanBackPath,
  useMealPlanEditor,
  type MealPlanDraft,
  type MealPlanEditorContext,
  type MealPlanStep,
} from '@/features/prescriptions/lib/meal-plan-draft';
import {
  EMPTY_CALCULATION,
  ageOnDate,
  calculationFromAssessment,
  calculationFromResult,
  normalizeCalculation,
  targetsFromResult,
  type Calculation,
  type FoodNutrients,
} from '@/features/prescriptions/lib/nutrition';
import { apiClient, unwrap } from '@/shared/api/client';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { EmptyState } from '@/shared/components/EmptyState';
import { ErrorState } from '@/shared/components/ErrorState';
import { FormField } from '@/shared/components/FormField';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Skeleton } from '@/shared/components/ui/skeleton';
import { StepIndicator } from '@/features/prescriptions/components/StepIndicator';

type PlanDetails = components['schemas']['MealPlanDetailsResponse'];

const NEW_PATH = '/trainer/meal-plans/new';

/** Rascunho inicial: vazio num plano novo, o plano gravado ao editar. */
function initialDraft(detail: PlanDetails | null, fixedClientId: string | null): MealPlanDraft {
  if (detail === null) {
    return {
      client: fixedClientId === null ? null : { id: fixedClientId, name: '' },
      name: '',
      description: '',
      startsDate: '',
      endsDate: '',
      calculation: EMPTY_CALCULATION,
      calculationDirty: false,
      preview: null,
      structure: { meals: [], plan_supplements: [] },
    };
  }
  return {
    client: { id: detail.client_id, name: '' },
    name: detail.name,
    description: detail.description ?? '',
    startsDate: detail.starts_date,
    endsDate: detail.ends_date ?? '',
    calculation: calculationFromResult(detail.calculation),
    calculationDirty: false,
    preview: detail.calculation,
    structure: mealStructureFromDetails(detail),
  };
}

/** Valores por 100 g, unidades e nomes que o detalhe já traz (o rascunho começa com eles). */
function seedFromDetail(detail: PlanDetails | null) {
  const foods: Record<string, FoodNutrients> = {};
  const units: Record<string, string> = {};
  const names: Record<string, string> = {};
  for (const meal of detail?.meals ?? []) {
    for (const item of meal.items) {
      foods[item.food_id] = {
        kcal: item.kcal_per100_g,
        protein: item.protein_per100_g,
        carbs: item.carbs_per100_g,
        fats: item.fats_per100_g,
        fiber: item.fiber_per100_g,
      };
      names[item.food_id] = item.food_name;
    }
    for (const item of meal.supplements) {
      units[item.supplement_id] = item.unit_of_measure;
      names[item.supplement_id] = item.supplement_name;
    }
  }
  for (const item of detail?.plan_supplements ?? []) {
    units[item.supplement_id] = item.unit_of_measure;
    names[item.supplement_id] = item.supplement_name;
  }
  return { foods, units, names };
}

/** Erro do preview: o texto genérico de gravação não serve aqui. */
function previewFailure(failure: unknown): string {
  return isApiProblem(failure)
    ? 'Não foi possível calcular. Revê os dados do cálculo.'
    : 'Não foi possível contactar o servidor. Tenta novamente.';
}

/**
 * `true` quando todos os alimentos e suplementos (das refeições e do plano) têm item do
 * catálogo e quantidade, e cada refeição tem nome.
 */
function structureComplete(structure: MealPlanDraft['structure']): boolean {
  return (
    structure.meals.every(
      (meal) =>
        meal.meal_type.trim() !== '' &&
        meal.items.every((item) => item.food_id !== '' && item.quantity_in_grams > 0) &&
        meal.supplements.every((item) => item.supplement_id !== '' && item.quantity > 0)
    ) && structure.plan_supplements.every((item) => item.supplement_id !== '' && item.quantity > 0)
  );
}

/**
 * Layout do editor de planos alimentares: `meal-plans/new` e `meal-plans/:planId/edit`.
 *
 * Guarda o rascunho em memória e entrega-o aos dois passos (`MealPlanTargetsStep`,
 * `MealPlanMealsStep`) por `useOutletContext`. Há uma só gravação, no fim do passo 2:
 * plano novo → `POST` com cabeçalho, cálculo e estrutura; plano existente → `PUT`. O aviso
 * «Sair sem guardar?» só dispara ao sair do editor com alterações: navegar entre passos,
 * ou sair depois de guardar, não o dispara. `?client_id=` fixa o cliente e define o regresso.
 */
export function MealPlanEditorLayout() {
  const { planId = null } = useParams();
  const [search] = useSearchParams();
  const fixedClientId = search.get('client_id');
  const detail = useMealPlanDetailQuery(planId);
  const listPath = mealPlanBackPath(fixedClientId);
  const backPath =
    planId === null
      ? listPath
      : `/trainer/meal-plans/${planId}${fixedClientId === null ? '' : `?client_id=${encodeURIComponent(fixedClientId)}`}`;
  const backLabel =
    planId !== null
      ? 'Voltar ao plano'
      : fixedClientId === null
        ? 'Voltar a planos alimentares'
        : 'Voltar à ficha do cliente';

  const back = (
    <Button asChild variant="ghost" size="sm" className="-ml-2 min-h-11 md:min-h-8">
      <Link to={backPath}>
        <ArrowLeft aria-hidden /> {backLabel}
      </Link>
    </Button>
  );

  if (planId !== null && detail.isPending) {
    return (
      <section className="space-y-6">
        {back}
        <Skeleton role="status" aria-label="A carregar plano alimentar…" className="h-80 w-full" />
      </section>
    );
  }
  if (planId !== null && detail.isError) {
    return (
      <section className="space-y-6">
        {back}
        {isApiProblem(detail.error) && detail.error.status === 404 ? (
          <EmptyState
            icon={UtensilsCrossed}
            title="Plano alimentar não encontrado"
            description="Este plano já não existe."
          />
        ) : (
          <ErrorState error={detail.error} onRetry={() => void detail.refetch()} />
        )}
      </section>
    );
  }

  return (
    <EditorBody
      key={planId ?? 'new'}
      planId={planId}
      detail={detail.data ?? null}
      fixedClientId={fixedClientId}
      back={back}
      listPath={listPath}
      backPath={backPath}
    />
  );
}

function EditorBody({
  planId,
  detail,
  fixedClientId,
  back,
  listPath,
  backPath,
}: {
  planId: string | null;
  detail: PlanDetails | null;
  fixedClientId: string | null;
  back: ReactNode;
  listPath: string;
  backPath: string;
}) {
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const seed = useMemo(() => seedFromDetail(detail), [detail]);
  const blocked = useMemo(
    () => (detail === null ? NO_BLOCKED_ITEMS : blockedItemsFromDetails(detail)),
    [detail]
  );
  const [draft, setDraft] = useState<MealPlanDraft>(() => initialDraft(detail, fixedClientId));
  const [names, setNames] = useState<Record<string, string>>(seed.names);
  const [dirty, setDirty] = useState(false);
  const [saved, setSaved] = useState<{ id: string } | null>(null);
  const [previewError, setPreviewError] = useState('');
  const [saveError, setSaveError] = useState('');
  const revision = useRef(0);

  const base = planId === null ? NEW_PATH : `/trainer/meal-plans/${planId}/edit`;
  const readOnly = detail?.is_archived === true;
  const clientId = draft.client?.id ?? '';
  const client = useNutritionClientQuery(clientId);
  const assessment = useNutritionAssessmentQuery(clientId, client.isSuccess);
  const clientAge =
    client.data === undefined ? null : ageOnDate(client.data.birth_date, new Date());

  const foodIds = draft.structure.meals.flatMap((meal) => meal.items.map((item) => item.food_id));
  const supplementIds = [
    ...draft.structure.meals.flatMap((meal) => meal.supplements.map((item) => item.supplement_id)),
    ...draft.structure.plan_supplements.map((item) => item.supplement_id),
  ];
  const foods = useFoodNutrients(foodIds, seed.foods);
  const units = useSupplementUnits(supplementIds, seed.units);

  // Aviso de saída: só quando se sai do editor. Entre passos o caminho continua sob `base`.
  const guardOn = dirty && saved === null && !readOnly;
  const blocker = useBlocker(
    ({ nextLocation }) => guardOn && !nextLocation.pathname.startsWith(base)
  );
  useEffect(() => {
    if (!guardOn) return;
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [guardOn]);

  // Depois de guardar, o guard já está desligado quando a navegação acontece.
  useEffect(() => {
    if (saved === null) return;
    void navigate(planId === null ? listPath : backPath, { replace: true });
  }, [saved, planId, listPath, backPath, navigate]);

  const preview = useMutation({
    mutationFn: async (input: { revision: number; calculation: Calculation }) =>
      unwrap(
        await apiClient.POST('/api/v1/nutrition/preview', {
          body: { calculation: normalizeCalculation(input.calculation) },
        })
      ),
    onSuccess: (result, input) => {
      // Um preview de um cálculo que já mudou não pode ser mostrado como o atual.
      if (input.revision !== revision.current) return;
      setDraft((current) => ({ ...current, preview: result }));
      setPreviewError('');
    },
    onError: (failure, input) => {
      if (input.revision !== revision.current) return;
      setPreviewError(previewFailure(failure));
    },
  });

  const save = useMutation({
    mutationFn: async () => {
      const metadata = {
        name: draft.name.trim(),
        description: draft.description.trim() || null,
        starts_date: draft.startsDate,
        ends_date: draft.endsDate || null,
        structure: draft.structure,
      };
      if (planId === null) {
        if (draft.client === null) throw new Error('client_required');
        return unwrap(
          await apiClient.POST('/api/v1/meal-plans', {
            body: {
              client_id: draft.client.id,
              ...metadata,
              calculation: normalizeCalculation(draft.calculation),
            },
          })
        );
      }
      return unwrap(
        await apiClient.PUT('/api/v1/meal-plans/{mealPlanId}', {
          params: { path: { mealPlanId: planId } },
          body: {
            ...metadata,
            calculation: draft.calculationDirty ? normalizeCalculation(draft.calculation) : null,
          },
        })
      );
    },
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: prescriptionKeys.meals });
      toast.success(planId === null ? 'Plano alimentar criado.' : 'Plano alimentar guardado.');
      setSaved({ id: result.id });
    },
    onError: (failure) => setSaveError(prescriptionError(failure)),
  });

  function touch(next: (current: MealPlanDraft) => MealPlanDraft) {
    setDraft(next);
    setDirty(true);
  }

  function changeCalculation(next: Calculation) {
    revision.current++;
    setPreviewError('');
    touch((current) => ({
      ...current,
      calculation: next,
      calculationDirty: true,
      preview: null,
    }));
  }

  const context: MealPlanEditorContext = {
    planId,
    readOnly,
    draft,
    clientFixed: planId !== null || fixedClientId !== null,
    clientName: client.data?.name ?? draft.client?.name ?? '',
    clientAge,
    clientLoading: client.isLoading || assessment.isLoading,
    clientError: client.error ?? assessment.error,
    retryClient: () => {
      if (client.isError) void client.refetch();
      if (assessment.isError) void assessment.refetch();
    },
    canUseAssessment: client.isSuccess && assessment.isSuccess,
    setField: (field, value) => touch((current) => ({ ...current, [field]: value })),
    chooseClient: (choice: ClientChoice) => {
      revision.current++;
      setPreviewError('');
      touch((current) => ({
        ...current,
        client: choice,
        calculation: EMPTY_CALCULATION,
        preview: null,
      }));
    },
    changeCalculation,
    applyAssessment: () => {
      if (client.data === undefined) return;
      changeCalculation(
        calculationFromAssessment(
          draft.calculation,
          client.data,
          assessment.data ?? null,
          new Date()
        )
      );
    },
    changeStructure: (structure) => touch((current) => ({ ...current, structure })),
    foods,
    units,
    names,
    blocked,
    nameItem: (id, name) => setNames((current) => ({ ...current, [id]: name })),
    hasValidTargets: draft.preview !== null,
    runPreview: () => {
      setPreviewError('');
      void preview
        .mutateAsync({ revision: revision.current, calculation: draft.calculation })
        .catch(() => undefined);
    },
    previewPending: preview.isPending,
    previewError,
    save: () => {
      setSaveError('');
      // Diz exatamente o que falta no passo das metas, em vez de uma lista genérica.
      const missing = [
        draft.name.trim() === '' ? 'o nome' : null,
        draft.startsDate === '' ? 'a data de início' : null,
        draft.preview === null ? 'calcular as metas' : null,
      ].filter((item): item is string => item !== null);
      if (missing.length > 0) {
        setSaveError(`Volta ao passo das metas: falta ${joinPt(missing)}.`);
        return;
      }
      if (!structureComplete(draft.structure)) {
        setSaveError(
          'Escolhe o alimento ou suplemento e indica a quantidade em todas as linhas, e dá nome a cada refeição.'
        );
        return;
      }
      void save.mutateAsync().catch(() => undefined);
    },
    savePending: save.isPending,
    saveError,
    targetsPath: `${base}${location.search}`,
    goToStep: (step: MealPlanStep) =>
      void navigate({
        pathname: step === 'meals' ? `${base}/meals` : base,
        search: location.search,
      }),
  };

  const onMeals = location.pathname.endsWith('/meals');

  return (
    <section className="space-y-6">
      {back}
      <PageHeader
        title={planId === null ? 'Criar plano alimentar' : 'Editar plano alimentar'}
        description="Primeiro as metas de kcal e macros, depois as refeições."
      />
      <StepIndicator step={onMeals ? 2 : 1} steps={['Metas', 'Refeições']} />
      {readOnly && (
        <p role="status" className="text-muted-foreground text-sm">
          Plano arquivado. Só consulta.
        </p>
      )}
      <Outlet context={context} />
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        title="Sair sem guardar?"
        description="As alterações a este plano alimentar perdem-se."
        confirmLabel="Sair sem guardar"
        cancelLabel="Continuar a editar"
        destructive
        pending={false}
        onConfirm={() => blocker.proceed?.()}
      />
    </section>
  );
}

/**
 * Passo 1 «Metas»: cliente, nome, descrição, datas e o cálculo de kcal e macros. «Calcular»
 * pede o preview ao servidor e «Seguinte» só avança com um preview do cálculo atual.
 */
export function MealPlanTargetsStep() {
  const editor = useMealPlanEditor();
  const { draft, readOnly } = editor;
  const [errors, setErrors] = useState<Record<string, string>>({});

  function validate(): boolean {
    const next: Record<string, string> = {};
    if (draft.client === null) next.client = 'Escolhe o cliente.';
    if (draft.name.trim() === '') next.name = 'Indica o nome do plano.';
    if (draft.startsDate === '') next.startsDate = 'Indica a data de início.';
    else if (draft.endsDate !== '' && draft.endsDate < draft.startsDate)
      next.endsDate = 'A data de fim não pode ser anterior à de início.';
    setErrors(next);
    return Object.keys(next).length === 0;
  }

  return (
    <form
      className="space-y-6"
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        if (validate()) editor.goToStep('meals');
      }}
    >
      <div className="space-y-4">
        {editor.clientFixed ? (
          <div className="flex flex-col gap-1 text-sm">
            <span>Cliente</span>
            <p className="font-medium">{editor.clientName === '' ? '…' : editor.clientName}</p>
          </div>
        ) : (
          <FormField label="Cliente" error={errors.client || undefined}>
            {(control) => (
              <ClientCombobox
                {...control}
                value={draft.client}
                onChange={(choice) => {
                  editor.chooseClient(choice);
                  setErrors((current) => ({ ...current, client: '' }));
                }}
              />
            )}
          </FormField>
        )}
        <FormField label="Nome" error={errors.name || undefined}>
          {(control) => (
            <Input
              {...control}
              value={draft.name}
              maxLength={255}
              disabled={readOnly}
              onChange={(event) => editor.setField('name', event.target.value)}
            />
          )}
        </FormField>
        <FormField label="Descrição">
          {(control) => (
            <Input
              {...control}
              value={draft.description}
              disabled={readOnly}
              onChange={(event) => editor.setField('description', event.target.value)}
            />
          )}
        </FormField>
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Data de início" error={errors.startsDate || undefined}>
            {(control) => (
              <Input
                {...control}
                type="date"
                value={draft.startsDate}
                disabled={readOnly}
                onChange={(event) => editor.setField('startsDate', event.target.value)}
              />
            )}
          </FormField>
          <FormField label="Data de fim" error={errors.endsDate || undefined}>
            {(control) => (
              <Input
                {...control}
                type="date"
                value={draft.endsDate}
                disabled={readOnly}
                onChange={(event) => editor.setField('endsDate', event.target.value)}
              />
            )}
          </FormField>
        </div>
      </div>

      {editor.clientLoading && <p role="status">A carregar dados do cliente…</p>}
      {editor.clientError !== null && editor.clientError !== undefined && (
        <ErrorState error={editor.clientError} onRetry={editor.retryClient} />
      )}

      <NutritionCalculationFields
        value={draft.calculation}
        disabled={readOnly}
        clientAge={editor.clientAge}
        canUseAssessment={editor.canUseAssessment}
        onUseAssessment={editor.applyAssessment}
        onChange={editor.changeCalculation}
      />

      <div className="space-y-3">
        {!readOnly && (
          <Button
            type="button"
            variant="outline"
            className="min-h-11 md:min-h-8"
            disabled={editor.previewPending}
            onClick={editor.runPreview}
          >
            {editor.previewPending ? 'A calcular…' : 'Calcular'}
          </Button>
        )}
        {editor.previewError !== '' && (
          <p role="alert" className="text-destructive text-sm">
            {editor.previewError}
          </p>
        )}
        {draft.preview === null ? (
          <p role="status" className="text-muted-foreground text-sm">
            Calcula as metas para continuar.
          </p>
        ) : (
          <TargetsSummary targets={targetsFromResult(draft.preview)} />
        )}
      </div>

      <div className="flex justify-end">
        <Button type="submit" className="min-h-11 md:min-h-8" disabled={!editor.hasValidTargets}>
          Seguinte
        </Button>
      </div>
    </form>
  );
}

/**
 * Passo 2 «Refeições»: refeições, alimentos e suplementos, com o painel fixo do que falta
 * para a meta. Num plano novo, sem o passo 1 válido volta ao passo 1.
 */
export function MealPlanMealsStep() {
  const editor = useMealPlanEditor();
  const { draft, readOnly } = editor;

  if (
    editor.planId === null &&
    (draft.client === null ||
      draft.name.trim() === '' ||
      draft.startsDate === '' ||
      draft.preview === null)
  ) {
    return <Navigate to={editor.targetsPath} replace />;
  }

  const allItems = draft.structure.meals.flatMap((meal) => meal.items);
  const totals = sumUnblockedFoods(allItems, editor.foods, editor.blocked);

  return (
    <div className="space-y-6">
      {draft.preview !== null && (
        <NutritionBalance targets={targetsFromResult(draft.preview)} totals={totals} sticky />
      )}
      <MealStructureEditor
        value={draft.structure}
        onChange={editor.changeStructure}
        disabled={readOnly}
        names={editor.names}
        onNamed={editor.nameItem}
        foods={editor.foods}
        units={editor.units}
        blocked={editor.blocked}
      />
      {editor.saveError !== '' && (
        <p role="alert" className="text-destructive text-sm">
          {editor.saveError}
        </p>
      )}
      <div className="flex flex-wrap justify-between gap-3">
        <Button
          type="button"
          variant="outline"
          className="min-h-11 md:min-h-8"
          onClick={() => editor.goToStep('targets')}
        >
          Voltar às metas
        </Button>
        {!readOnly && (
          <Button
            type="button"
            className="min-h-11 md:min-h-8"
            disabled={editor.savePending}
            onClick={editor.save}
          >
            {editor.planId === null
              ? editor.savePending
                ? 'A criar…'
                : 'Criar'
              : editor.savePending
                ? 'A guardar…'
                : 'Guardar'}
          </Button>
        )}
      </div>
    </div>
  );
}

/** «a», «a e b», «a, b e c». */
function joinPt(items: readonly string[]): string {
  return items.length <= 1
    ? (items[0] ?? '')
    : `${items.slice(0, -1).join(', ')} e ${items[items.length - 1] ?? ''}`;
}
