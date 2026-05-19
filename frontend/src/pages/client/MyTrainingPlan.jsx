/**
 * MyTrainingPlan.jsx - plano de treino ativo do cliente.
 *
 * A página mostra o planeado pelo Personal Trainer e permite registar a
 * execução real do aluno por série.
 *
 * Separação importante de domínio:
 * - `set_loads` = carga planeada pelo PT
 * - `client_set_logs` = carga real executada pelo cliente
 *
 * O endpoint de logs funciona em modo de substituição completa: o payload
 * enviado pelo frontend passa a ser a fonte de verdade para as séries do
 * exercício, incluindo o caso `logs: []` para apagar os registros existentes.
 */

import { useEffect, useState } from 'react';
import { toast } from 'react-toastify';
import {
  getMyTrainingPlan,
  upsertExerciseSetLogs,
} from '@/api/clientPortalApi';
import {
  Dumbbell,
  ChevronDown,
  ChevronUp,
  ExternalLink,
  Save,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  formatDate,
  parseNullableFloat,
  parseNullableInt,
} from '@/utils/formatters';

function formatRestRange(value) {
  if (!value) return null;

  // Se a unidade ja vier no valor, devolvemos como esta.
  if (/[a-zA-Z]/.test(value)) return value;

  return `${value}s`;
}

function buildExerciseDrafts(exercise) {
  // O frontend precisa de uma linha por série, mesmo quando ainda não existem logs.
  // Por isso combinamos:
  // - metadata do exercício (sets)
  // - logs reais existentes do cliente
  // - carga planeada do PT, apenas para mostrar como referência
  const logsBySetNumber = new Map(
    (exercise.client_set_logs ?? []).map((log) => [log.set_number, log])
  );
  const plannedBySetNumber = new Map(
    (exercise.set_loads ?? exercise.sets_loads ?? []).map((item) => [
      item.set_number,
      item,
    ])
  );

  return Array.from({ length: exercise.sets ?? 0 }, (_, index) => {
    const setNumber = index + 1;
    const savedLog = logsBySetNumber.get(setNumber);
    const plannedLoad = plannedBySetNumber.get(setNumber);

    return {
      set_number: setNumber,
      planned_weight_kg: plannedLoad?.planned_weight_kg ?? null,
      weight_kg:
        savedLog?.weight_kg !== null && savedLog?.weight_kg !== undefined
          ? String(savedLog.weight_kg)
          : '',
      reps_done:
        savedLog?.reps_done !== null && savedLog?.reps_done !== undefined
          ? String(savedLog.reps_done)
          : '',
      notes: savedLog?.notes ?? '',
      logged_at: savedLog?.logged_at ?? null,
      updated_at: savedLog?.updated_at ?? null,
    };
  });
}

function buildInitialExerciseForms(planData) {
  const forms = {};

  // Prepara o estado local de todos os exercícios logo após o carregamento.
  for (const day of planData?.days ?? []) {
    for (const exercise of day.exercises ?? []) {
      forms[exercise.id] = buildExerciseDrafts(exercise);
    }
  }

  return forms;
}

function mergeExerciseLogsIntoPlan(planData, planDayExerciseId, savedLogs) {
  if (!planData) return planData;

  // Atualiza apenas o exercício guardado sem obrigar a novo pedido ao backend.
  return {
    ...planData,
    days: planData.days.map((day) => ({
      ...day,
      exercises: day.exercises.map((exercise) =>
        exercise.id === planDayExerciseId
          ? {
              ...exercise,
              client_set_logs: savedLogs,
            }
          : exercise
      ),
    })),
  };
}

export default function MyTrainingPlan() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  // O estado de expansao usa o indice do dia como chave.
  const [expandedDays, setExpandedDays] = useState({});
  // Cada exercício guarda um rascunho local por série até ser submetido.
  const [exerciseForms, setExerciseForms] = useState({});
  // Controla se a área de registro de execução de cada exercício está aberta.
  const [expandedExerciseLogs, setExpandedExerciseLogs] = useState({});
  // Guarda qual exercício está a ser persistido para bloquear apenas esse botão.
  const [savingExerciseId, setSavingExerciseId] = useState(null);

  useEffect(() => {
    getMyTrainingPlan()
      .then((planData) => {
        setData(planData);
        setExerciseForms(buildInitialExerciseForms(planData));

        // Abre o primeiro dia por defeito para reduzir atrito inicial.
        if (planData?.days?.length > 0) {
          setExpandedDays({ 0: true });
        }
      })
      .catch((error) => {
        toast.error(
          error?.response?.data?.detail ?? 'Erro ao carregar plano de treino'
        );
      })
      .finally(() => setLoading(false));
  }, []);

  function toggleDay(index) {
    setExpandedDays((prev) => ({
      ...prev,
      [index]: !prev[index],
    }));
  }

  function toggleExerciseLogs(exerciseId) {
    setExpandedExerciseLogs((prev) => ({
      ...prev,
      [exerciseId]: !prev[exerciseId],
    }));
  }

  function updateExerciseField(exerciseId, setNumber, field, value) {
    // Atualiza apenas a série editada e preserva o resto do formulário.
    setExerciseForms((prev) => ({
      ...prev,
      [exerciseId]: (prev[exerciseId] ?? []).map((row) =>
        row.set_number === setNumber ? { ...row, [field]: value } : row
      ),
    }));
  }

  function clearExerciseDraft(exercise) {
    // Limpa o rascunho local e prepara o payload vazio para apagar logs ao guardar.
    setExerciseForms((prev) => ({
      ...prev,
      [exercise.id]: buildExerciseDrafts({
        ...exercise,
        client_set_logs: [],
      }),
    }));
  }

  async function handleSaveExercise(exercise) {
    const draftRows = exerciseForms[exercise.id] ?? [];

    // O endpoint agora funciona por substituição completa:
    // tudo o que não vier no payload será removido no backend.
    const payloadLogs = draftRows
      .map((row) => ({
        set_number: row.set_number,
        weight_kg: parseNullableFloat(row.weight_kg),
        reps_done: parseNullableInt(row.reps_done),
        notes: row.notes.trim() || null,
      }))
      .filter(
        (row) =>
          row.weight_kg !== null || row.reps_done !== null || row.notes !== null
      );

    setSavingExerciseId(exercise.id);

    try {
      const savedLogs = await upsertExerciseSetLogs(exercise.id, {
        logs: payloadLogs,
      });

      // Atualizamos os dados do plano para a UI refletir imediatamente
      // o estado persistido no backend.
      const nextPlanData = mergeExerciseLogsIntoPlan(
        data,
        exercise.id,
        savedLogs
      );
      setData(nextPlanData);
      setExerciseForms((prev) => ({
        ...prev,
        [exercise.id]: buildExerciseDrafts({
          ...exercise,
          client_set_logs: savedLogs,
        }),
      }));

      toast.success(
        payloadLogs.length === 0
          ? 'Registos do exercício removidos com sucesso.'
          : 'Registo do exercício guardado com sucesso.'
      );
    } catch (error) {
      toast.error(
        error?.response?.data?.detail ?? 'Erro ao guardar registo do exercício'
      );
    } finally {
      setSavingExerciseId(null);
    }
  }

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-64">
        <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
      </div>
    );
  }

  if (!data?.plan) {
    return (
      <div className="p-4 lg:p-6">
        <h1 className="text-2xl font-semibold text-foreground mb-2">
          Plano de Treino
        </h1>
        <div className="rounded-lg border border-border bg-card p-8 flex flex-col items-center gap-3 text-center">
          <Dumbbell className="h-10 w-10 text-muted-foreground" />
          <p className="font-medium text-muted-foreground">
            Sem plano de treino ativo
          </p>
          <p className="text-sm text-muted-foreground">
            O teu Personal Trainer ainda não te atribuiu um plano de treino.
            Assim que o fizer, poderás consultá-lo aqui.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">
          Plano de Treino
        </h1>
        <p className="text-sm text-muted-foreground mt-1">{data.plan.name}</p>
      </div>

      {(data.plan.start_date || data.plan.notes) && (
        <Card className="border-border bg-card">
          <CardContent className="pt-4 flex flex-col gap-2">
            {data.plan.start_date && (
              <p className="text-sm text-muted-foreground">
                Início:{' '}
                <span className="text-foreground">{data.plan.start_date}</span>
                {data.plan.end_date && (
                  <>
                    {' '}
                    | Fim:{' '}
                    <span className="text-foreground">
                      {data.plan.end_date}
                    </span>
                  </>
                )}
              </p>
            )}
            {data.plan.notes && (
              <p className="text-sm text-muted-foreground">{data.plan.notes}</p>
            )}
          </CardContent>
        </Card>
      )}

      <div className="flex flex-col gap-3">
        {data.days.map((day, index) => (
          <Card key={day.id} className="border-border bg-card overflow-hidden">
            {/* O cabeçalho do dia funciona como gatilho para abrir e fechar a secção. */}
            <button
              onClick={() => toggleDay(index)}
              className="w-full text-left"
            >
              <CardHeader className="pb-3">
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle className="text-base">{day.name}</CardTitle>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      {day.exercises.length} exercício
                      {day.exercises.length !== 1 ? 's' : ''}
                    </p>
                    {day.notes && (
                      <p className="text-xs text-muted-foreground mt-1">
                        {day.notes}
                      </p>
                    )}
                  </div>
                  {expandedDays[index] ? (
                    <ChevronUp className="h-4 w-4 text-muted-foreground" />
                  ) : (
                    <ChevronDown className="h-4 w-4 text-muted-foreground" />
                  )}
                </div>
              </CardHeader>
            </button>

            {expandedDays[index] && (
              <CardContent className="pt-0 flex flex-col gap-3">
                {day.exercises.map((exercise, exerciseIndex) => {
                  // Se nao houver logs guardados, usamos o rascunho gerado a partir do planeado.
                  const draftRows = exerciseForms[exercise.id] ?? [];
                  const isSaving = savingExerciseId === exercise.id;
                  const isLogsExpanded = !!expandedExerciseLogs[exercise.id];

                  return (
                    <div
                      key={exercise.id}
                      className="rounded-lg border border-border bg-background p-3 flex flex-col gap-4"
                    >
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-2">
                            <span className="text-xs text-muted-foreground font-mono">
                              {exerciseIndex + 1}.
                            </span>
                            <p className="text-sm font-medium text-foreground">
                              {exercise.exercise_name}
                            </p>
                            {exercise.is_superset_group && (
                              <Badge variant="secondary" className="text-xs">
                                Superset
                              </Badge>
                            )}
                          </div>
                          {exercise.exercise_muscles && (
                            <p className="text-xs text-muted-foreground mt-0.5 ml-5">
                              {exercise.exercise_muscles}
                            </p>
                          )}
                          {exercise.notes && (
                            <p className="text-xs text-muted-foreground mt-1 ml-5">
                              Nota do PT: {exercise.notes}
                            </p>
                          )}
                        </div>
                        {exercise.exercise_url && (
                          <a
                            href={exercise.exercise_url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-primary hover:text-primary/80 shrink-0"
                          >
                            <ExternalLink className="h-4 w-4" />
                          </a>
                        )}
                      </div>

                      <div className="flex flex-wrap gap-3 ml-5 text-xs text-muted-foreground">
                        {exercise.sets && (
                          <span>
                            <span className="text-foreground font-medium">
                              {exercise.sets}
                            </span>{' '}
                            séries
                          </span>
                        )}
                        {exercise.reps_range && (
                          <span>
                            <span className="text-foreground font-medium">
                              {exercise.reps_range}
                            </span>{' '}
                            repetições
                          </span>
                        )}
                        {exercise.rest_range_seconds && (
                          <span>
                            Descanso:{' '}
                            <span className="text-foreground font-medium">
                              {formatRestRange(exercise.rest_range_seconds)}
                            </span>
                          </span>
                        )}
                        {exercise.tempo && (
                          <span>
                            Tempo:{' '}
                            <span className="text-foreground font-medium">
                              {exercise.tempo}
                            </span>
                          </span>
                        )}
                      </div>

                      <div className="rounded-lg border border-border bg-card/60 p-3 flex flex-col gap-3">
                        <div className="flex items-center justify-between gap-3">
                          {/* O bloco pode ser recolhido para reduzir ruido visual quando o cliente esta apenas a consultar o plano. */}
                          <button
                            type="button"
                            onClick={() => toggleExerciseLogs(exercise.id)}
                            className="flex flex-1 items-center justify-between gap-3 text-left"
                          >
                            <div>
                              <p className="text-sm font-medium text-foreground">
                                Registo de execução
                              </p>
                              <p className="text-xs text-muted-foreground">
                                Planeado pelo PT e executado por ti ficam
                                separados.
                              </p>
                            </div>
                            {isLogsExpanded ? (
                              <ChevronUp className="h-4 w-4 text-muted-foreground shrink-0" />
                            ) : (
                              <ChevronDown className="h-4 w-4 text-muted-foreground shrink-0" />
                            )}
                          </button>

                          <div className="flex items-center gap-2">
                            <Button
                              type="button"
                              variant="outline"
                              size="sm"
                              onClick={() => clearExerciseDraft(exercise)}
                              disabled={isSaving}
                            >
                              Limpar
                            </Button>

                            {/* O guardado e feito por exercicio para nao afetar os restantes blocos. */}
                            <Button
                              type="button"
                              size="sm"
                              onClick={() => handleSaveExercise(exercise)}
                              disabled={isSaving}
                            >
                              <Save className="h-4 w-4 mr-2" />
                              {isSaving ? 'A guardar...' : 'Guardar'}
                            </Button>
                          </div>
                        </div>

                        {isLogsExpanded && (
                          <div className="grid gap-3">
                            {draftRows.map((row) => (
                              <div
                                key={row.set_number}
                                className="rounded-md border border-border bg-background p-3"
                              >
                                {/* Cada cartão representa uma série com o planeado e o executado. */}
                                <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
                                  <p className="text-sm font-medium text-foreground">
                                    Série {row.set_number}
                                  </p>
                                  <div className="flex flex-wrap gap-2 text-xs">
                                    <span className="rounded bg-muted px-2 py-1 text-muted-foreground">
                                      Planeado:{' '}
                                      <span className="text-foreground font-medium">
                                        {row.planned_weight_kg !== null &&
                                        row.planned_weight_kg !== undefined
                                          ? `${row.planned_weight_kg} kg`
                                          : '-'}
                                      </span>
                                    </span>
                                    {row.updated_at && (
                                      <span className="rounded bg-primary/10 px-2 py-1 text-primary">
                                        Último registo:{' '}
                                        {formatDate(row.updated_at)}
                                      </span>
                                    )}
                                  </div>
                                </div>

                                <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                                  <div className="flex flex-col gap-1.5">
                                    <label className="text-xs font-medium text-muted-foreground">
                                      Carga real (kg)
                                    </label>
                                    <Input
                                      type="number"
                                      min="0"
                                      step="0.5"
                                      placeholder="Ex: 72.5"
                                      value={row.weight_kg}
                                      onChange={(event) =>
                                        updateExerciseField(
                                          exercise.id,
                                          row.set_number,
                                          'weight_kg',
                                          event.target.value
                                        )
                                      }
                                    />
                                  </div>

                                  <div className="flex flex-col gap-1.5">
                                    <label className="text-xs font-medium text-muted-foreground">
                                      Repetições feitas
                                    </label>
                                    <Input
                                      type="number"
                                      min="0"
                                      step="1"
                                      placeholder="Ex: 10"
                                      value={row.reps_done}
                                      onChange={(event) =>
                                        updateExerciseField(
                                          exercise.id,
                                          row.set_number,
                                          'reps_done',
                                          event.target.value
                                        )
                                      }
                                    />
                                  </div>

                                  <div className="flex flex-col gap-1.5 md:col-span-1">
                                    <label className="text-xs font-medium text-muted-foreground">
                                      Notas
                                    </label>
                                    <Textarea
                                      rows={2}
                                      placeholder="Observações da série"
                                      value={row.notes}
                                      onChange={(event) =>
                                        updateExerciseField(
                                          exercise.id,
                                          row.set_number,
                                          'notes',
                                          event.target.value
                                        )
                                      }
                                    />
                                  </div>
                                </div>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    </div>
                  );
                })}
              </CardContent>
            )}
          </Card>
        ))}
      </div>
    </div>
  );
}
