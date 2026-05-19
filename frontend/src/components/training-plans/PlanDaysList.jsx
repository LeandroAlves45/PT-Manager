import { useState } from 'react';
import { toast } from 'react-toastify';
import {
  createPlanDay,
  deletePlanDay,
  addExerciseToDay,
  deleteDayExercise,
  updateDayExercise,
} from '@/api/trainingPlan';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import ExercisePicker from './ExercisePicker';
import {
  Plus,
  Trash2,
  Dumbbell,
  GripVertical,
  Check,
  X,
  ExternalLink,
} from 'lucide-react';

/**
 * * Lista os dias de um plano com os seus exercícios.
 *
 * Estrutura de dados:
 * Plan → Days[] → DayExercises[] → Exercise (da biblioteca)
 *
 * Cada dia tem um Accordion que expande para mostrar os exercícios.
 * Dentro de cada dia há um botão para abrir o ExercisePicker.
 *
 * @param {string} planId - ID do plano (para chamar a API)
 * @param {Array} days - Lista de dias já carregados pelo pai
 * @param {Function} onRefresh - Chamada após mutações para re-fetch dos dias
 */

export default function PlanDaysList({ planId, days = [], onRefresh }) {
  // Controla quais dias estão com o accordion aberto (preserva estado após refresh)
  const [openDays, setOpenDays] = useState([]);

  const [newdayName, setNewDayName] = useState('');
  const [addingDay, setAddingDay] = useState(false);

  //controla qual dia esta a receber um exercicio
  const [pickerDayId, setPickerDayId] = useState(null);

  //Controla qual exercicio está em modo edição
  const [editingExerciseId, setEditingExerciseId] = useState(null);

  //Valores temporarios para edição de exercício
  const [editValues, setEditValues] = useState({
    sets: '',
    reps_range: '',
    rest_range_seconds: '',
  });

  //Ativa o modo de edição para um exercício específico, preenchendo os campos com os valores atuais
  const handleStartEdit = (de) => {
    setEditingExerciseId(de.id);
    setEditValues({
      sets: de.sets,
      reps_range: de.reps_range,
      rest_range_seconds: de.rest_range_seconds ?? '',
      notes: de.notes ?? '',
    });
  };

  //Cancela a edição sem guardar
  const handleCancelEdit = () => {
    setEditingExerciseId(null);
    setEditValues({
      sets: '',
      reps_range: '',
      rest_range_seconds: '',
      notes: '',
    });
  };

  //Guarda as alterações editando o exercício via API
  const handleConfirmEdit = async (de) => {
    //validação simples
    const setsNum = parseInt(editValues.sets);
    if (isNaN(setsNum) || setsNum <= 0) {
      toast.error('Séries deve ser um número positivo.');
      return;
    }
    const repsRange = editValues.reps_range.trim();
    if (!repsRange) {
      toast.error('Intervalo de repetições é obrigatório.');
      return;
    }

    try {
      //só envia campos alterados
      await updateDayExercise(de.id, {
        sets: setsNum,
        reps_range: editValues.reps_range.trim(),
        rest_range_seconds: editValues.rest_range_seconds
          ? editValues.rest_range_seconds.trim()
          : null,
        notes: editValues.notes ? editValues.notes.trim() : null,
      });
      toast.success('Exercício atualizado!');
      setEditingExerciseId(null);
      onRefresh();
    } catch (error) {
      toast.error('Erro ao atualizar exercício.');
    }
  };

  // Criar Dia
  const handleAddDay = async () => {
    if (!newdayName.trim()) return;
    try {
      setAddingDay(true);
      await createPlanDay(planId, {
        name: newdayName.trim(),
        order_index: days.length + 1,
      });
      setNewDayName('');
      toast.success('Dia adicionado!');
      onRefresh();
    } catch (error) {
      toast.error('Erro ao adicionar dia.');
    } finally {
      setAddingDay(false);
    }
  };

  // Deletar Dia
  const handleDeleteDay = async (dayId, dayName) => {
    if (!confirm(`Remover "${dayName}" e todos os seus exercícios?`)) return;
    try {
      await deletePlanDay(dayId);
      toast.success('Dia removido!');
      onRefresh();
    } catch (error) {
      toast.error('Erro ao remover dia.');
    }
  };

  // Adicionar exercício a um dia
  // O ExercisePicker chama esta função ao selecionar um exercício
  const handleExerciseSelect = async (exercise) => {
    if (!pickerDayId) return;
    try {
      //Conta exercicios ka existente para definir order_index do novo exercicio
      const day = days.find((d) => d.id === pickerDayId);
      const currentCount = day?.exercises?.length ?? 0;

      await addExerciseToDay(pickerDayId, {
        plan_day_id: pickerDayId,
        exercise_id: exercise.id,
        order_index: currentCount + 1,
        sets: 3, //pode ser fixo ou pedir input ao user
        reps_range: '12-8', //pode ser fixo ou pedir input ao user
      });
      toast.success(`${exercise.name} adicionado!`);
      onRefresh();
    } catch (error) {
      toast.error('Erro ao adicionar exercício.');
    } finally {
      setPickerDayId(null);
    }
  };

  // Deletar exercício de um dia
  const handleDeleteExercise = async (dayId, dayexerciseId, exerciseName) => {
    if (!confirm(`Remover ${exerciseName} deste dia?`)) return;
    try {
      await deleteDayExercise(dayexerciseId);
      toast.success('Exercício removido!');
      onRefresh();
    } catch (error) {
      toast.error('Erro ao remover exercício.');
    }
  };

  return (
    <div className="flex flex-col gap-4">
      {days.length === 0 ? (
        <p className="text-center text-muted-foreground py-8 text-sm">
          Nenhum dia adicionado ainda. Cria o primeiro dia abaixo.
        </p>
      ) : (
        <Accordion
          type="multiple"
          value={openDays}
          onValueChange={setOpenDays}
          className="flex flex-col gap-2"
        >
          {days.map((day) => (
            <AccordionItem
              key={day.id}
              value={day.id}
              className="border border-border rounded-lg overflow-hidden bg-card"
            >
              <AccordionTrigger className="px-4 py-3 hover:bg-accent/50 [&>svg]:text-muted-foreground">
                <div className="flex items-center gap-3 flex-1 mr-2">
                  <GripVertical className="h-4 w-4 text-muted-foreground shrink-0" />
                  <span className="text-sm font-medium text-foreground">
                    {day.name}
                  </span>
                  <Badge
                    variant="outline"
                    className="ml-auto mr-2 border-border text-muted-foreground text-xs"
                  >
                    {day.exercises?.length ?? 0} exercício
                    {(day.exercises?.length ?? 0) !== 1 ? 's' : ''}
                  </Badge>
                </div>
              </AccordionTrigger>

              <AccordionContent className="px-4 pb-4">
                {!day.exercises || day.exercises.length === 0 ? (
                  <p className="text-xs text-muted-foreground py-3 text-center">
                    Nenhum exercício adicionado ainda.
                  </p>
                ) : (
                  <div className="flex flex-col gap-3 mb-3">
                    {day.exercises.map((de) => {
                      // Verifica se ESTE exercício específico está em modo edição
                      const isEditing = editingExerciseId === de.id;

                      return (
                        <div
                          key={de.id}
                          className="flex items-center gap-3 rounded-lg border border-border p-3 bg-background"
                        >
                          <div className="h-8 w-8 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
                            <Dumbbell className="h-4 w-4 text-primary" />
                          </div>

                          <div className="flex-1 min-w-0">
                            <p className="text-sm font-medium text-foreground truncate">
                              {de.exercise_name ?? 'Exercício'}
                            </p>

                            {/* Modo visualização: clica para editar */}
                            {!isEditing ? (
                              <button
                                // Clique na linha de detalhes ativa edição inline
                                onClick={() => handleStartEdit(de)}
                                className="text-xs text-muted-foreground hover:text-primary transition-colors cursor-pointer"
                                title="Clica para editar séries e reps"
                              >
                                {/* Linha 1: séries e reps */}
                                <span>
                                  {de.sets} séries · {de.reps_range} reps
                                  <span className="ml-1 opacity-50">
                                    (editar)
                                  </span>
                                </span>
                                {/* Linha 2: descanso — só aparece se tiver valor */}
                                <span className="block text-muted-foreground/70 mt-0.5">
                                  ⏱{' '}
                                  {de.rest_range_seconds ? (
                                    `${de.rest_range_seconds}s descanso`
                                  ) : (
                                    <span className="italic opacity-60">
                                      sem descanso definido
                                    </span>
                                  )}
                                </span>

                                {/* Notas */}
                                <span className="block text-muted-foreground/70 italic mt-0.5">
                                  {de.notes ? (
                                    <span className="whitespace-pre-wrap">
                                      📝 {de.notes}
                                    </span>
                                  ) : (
                                    <span className="opacity-60">
                                      📝 sem notas
                                    </span>
                                  )}
                                </span>
                              </button>
                            ) : (
                              /* Modo edição: inputs inline */
                              <div className="flex items-center gap-1 mt-1">
                                {/* Input para séries — só aceita números */}
                                <Input
                                  type="number"
                                  min="1"
                                  max="20"
                                  value={editValues.sets}
                                  onChange={(e) =>
                                    setEditValues((prev) => ({
                                      ...prev,
                                      sets: e.target.value,
                                    }))
                                  }
                                  // Enter confirma, Escape cancela
                                  onKeyDown={(e) => {
                                    if (e.key === 'Enter')
                                      handleConfirmEdit(de);
                                    if (e.key === 'Escape') handleCancelEdit();
                                  }}
                                  className="h-6 w-14 text-xs px-1 bg-background"
                                  placeholder="Séries"
                                />
                                <span className="text-xs text-muted-foreground">
                                  ×
                                </span>
                                {/* Input para reps — aceita ranges como "8-12" */}
                                <Input
                                  type="text"
                                  value={editValues.reps_range}
                                  onChange={(e) =>
                                    setEditValues((prev) => ({
                                      ...prev,
                                      reps_range: e.target.value,
                                    }))
                                  }
                                  onKeyDown={(e) => {
                                    if (e.key === 'Enter')
                                      handleConfirmEdit(de);
                                    if (e.key === 'Escape') handleCancelEdit();
                                  }}
                                  className="h-6 w-16 text-xs px-1 bg-background"
                                  placeholder="8-12"
                                />

                                {/* Linha 2: descanso entre séries */}
                                <div className="flex items-center gap-1">
                                  <span className="text-xs text-muted-foreground w-14 shrink-0">
                                    ⏱ descanso
                                  </span>
                                  <Input
                                    type="text"
                                    value={editValues.rest_range_seconds}
                                    onChange={(e) =>
                                      setEditValues((prev) => ({
                                        ...prev,
                                        rest_range_seconds: e.target.value,
                                      }))
                                    }
                                    onKeyDown={(e) => {
                                      if (e.key === 'Enter')
                                        handleConfirmEdit(de);
                                      if (e.key === 'Escape')
                                        handleCancelEdit();
                                    }}
                                    className="h-6 w-20 text-xs px-1 bg-background"
                                    placeholder="60-90"
                                  />
                                </div>

                                {/* Notas — textarea para texto mais longo */}
                                <div className="flex items-center gap-1">
                                  <span className="text-xs text-muted-foreground w-16 shrink-0 mt-1">
                                    {' '}
                                    📝 notas
                                  </span>
                                  <textarea
                                    value={editValues.notes}
                                    onChange={(e) =>
                                      setEditValues((prev) => ({
                                        ...prev,
                                        notes: e.target.value,
                                      }))
                                    }
                                    onKeyDown={(e) => {
                                      if (e.key === 'Enter') {
                                        e.preventDefault();
                                        handleConfirmEdit(de);
                                      }
                                      if (e.key === 'Escape')
                                        handleCancelEdit();
                                    }}
                                    className="h-14 w-full text-xs px-2 py-1 bg-background border border-input rounded-md resize-none text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
                                    placeholder="Observações, dicas, variações, etc"
                                  />
                                </div>
                                {/* Confirmar*/}
                                <div className="flex items-center gap-1 ml-0.5">
                                  <button
                                    onClick={() => handleConfirmEdit(de)}
                                    className="text-green-500 hover:text-green-400 p-0.5"
                                    title="Confirmar (Enter)"
                                  >
                                    <Check className="h-3.5 w-3.5" />
                                  </button>
                                  {/* Cancelar */}
                                  <button
                                    onClick={handleCancelEdit}
                                    className="text-muted-foreground hover:text-foreground p-0.5"
                                    title="Cancelar (Esc)"
                                  >
                                    <X className="h-3.5 w-3.5" />
                                  </button>
                                </div>
                              </div>
                            )}
                          </div>

                          {/* URL do exercício — fora do button para o clique abrir o link */}
                          {de.exercise_url && (
                            <a
                              href={de.exercise_url}
                              target="_blank" // abre em nova aba
                              rel="noopener noreferrer" // segurança: impede acesso ao window.opener
                              onClick={(e) => e.stopPropagation()} // impede que active o accordion
                              className="inline-flex items-center gap-1 text-xs text-primary hover:underline mt-1"
                            >
                              <ExternalLink className="h-3 w-3" />
                              Ver demonstração
                            </a>
                          )}

                          {/* Botão delete — escondido durante edição para não confundir */}
                          {!isEditing && (
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-7 w-7 text-muted-foreground hover:text-destructive"
                              onClick={() =>
                                handleDeleteExercise(
                                  day.id,
                                  de.id,
                                  de.exercise_name ?? 'exercício'
                                )
                              }
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}

                <div className="flex items-center justify-between">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPickerDayId(day.id)}
                    className="gap-2 border-border text-muted-foreground hover:text-foreground text-xs"
                  >
                    <Plus className="h-3.5 w-3.5" />
                    Adicionar exercício
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => handleDeleteDay(day.id, day.name)}
                    className="text-destructive hover:text-destructive hover:bg-destructive/10 text-xs gap-2"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    Remover dia
                  </Button>
                </div>
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      )}

      <div className="flex items-center gap-2 mt-2">
        <Input
          placeholder="Nome do novo dia (ex: Pernas, Peito, etc)"
          value={newdayName}
          onChange={(e) => setNewDayName(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleAddDay()}
          className="bg-background border-input"
        />
        <Button
          onClick={handleAddDay}
          disabled={!newdayName.trim() || addingDay}
          className="bg-primary text-primary-foreground hover:bg-primary/90 gap-2 shrink-0"
        >
          Adicionar Dia
        </Button>
      </div>

      <ExercisePicker
        open={Boolean(pickerDayId)}
        onOpenChange={(isOpen) => !isOpen && setPickerDayId(null)}
        onSelect={handleExerciseSelect}
      />
    </div>
  );
}
