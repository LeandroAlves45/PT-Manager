/**
 * AssessmentsPage.jsx — avaliações iniciais e check-ins de progresso.
 *
 * Estrutura da página:
 *   Selector de cliente (dropdown no topo, partilhado pelos tabs)
 *   Tab "Avaliações Iniciais" — biometria e questionário de saúde
 *   Tab "Check-ins"           — progresso periódico pedido pelo Personal Trainer
 *   Tab "Progresso"           — gráficos de peso e % gordura ao longo do tempo
 *
 * Regras de negócio:
 *   - Uma avaliação inicial é criada pelo Personal Trainer na primeira sessão.
 *     Pode haver mais do que uma por cliente (reavaliações periódicas).
 *   - Um check-in é um pedido enviado ao cliente para ele reportar
 *     o seu progresso (peso, sensações, aderência ao plano).
 *     O Personal Trainer pode ignorá-lo ou adicionar notas após a resposta.
 *   - Os gráficos de progresso agregam os check-ins respondidos que contêm
 *     dados biométricos (peso e/ou % de gordura), ordenados por data de resposta.
 */

import { useState, useEffect, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';
import { formatDate } from '@/utils/formatters';
import { parseNullableFloat, parseNullableInt } from '@/utils/formatters';
import { getClients } from '@/api/clientsApi';
import {
  getAssessmentsByClient,
  createAssessment,
  updateAssessment,
  getCheckinsByClient,
  createCheckin,
  skipCheckin,
  addCheckinNotes,
} from '@/api/assessmentsApi';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/card';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Plus,
  ChevronDown,
  ChevronRight,
  ClipboardList,
  Activity,
  Scale,
  MessageSquare,
  SkipForward,
  Edit,
  User,
  TrendingUp,
} from 'lucide-react';
import { cn } from '@/lib/utils';

// =============================================================================
// Helpers de UI
// =============================================================================

/**
 * Badge de estado para check-ins — cor diferente por estado.
 * pending   → amarelo
 * completed → verde
 * skipped   → cinzento
 */
function CheckinStatusBadge({ status }) {
  const config = {
    pending: {
      label: 'Pendente',
      className: 'bg-yellow-50 text-yellow-700 border-yellow-200',
    },
    completed: {
      label: 'Respondido',
      className: 'bg-green-50 text-green-700 border-green-200',
    },
    skipped: {
      label: 'Ignorado',
      className: 'bg-muted text-muted-foreground border-border',
    },
  };
  const { label, className } = config[status] || config.skipped;
  return (
    <Badge variant="outline" className={cn('text-xs', className)}>
      {label}
    </Badge>
  );
}

/**
 * Formata um valor numérico de escala (1-5) com estrelas ou texto.
 * Usado para mostrar níveis de energia, stress, recuperação.
 */
function ScaleValue({ value, max = 5 }) {
  if (value == null) return <span className="text-muted-foreground">N/A</span>;
  return (
    <span className="font-medium">
      {value} / {max}
    </span>
  );
}

/**
 * Tooltip personalizado para o gráfico Recharts.
 * O Recharts fornece um Tooltip genérico, mas o aspeto não corresponde
 * ao design system do PT Manager (shadcn/ui). Este componente substitui-o
 * por um card estilizado com as variáveis CSS do tema (--card, --border, etc.).
 *
 * Props injectadas automaticamente pelo Recharts:
 *   active  — true quando o cursor está sobre um ponto do gráfico
 *   payload — array com os dados da série naquele ponto: [{ name, value, color }]
 *   label   — valor do eixo X naquele ponto (a data formatada)
 */
function ProgressChartTooltip({ active, payload, label }) {
  // Se não está ativo ou nãi há dados, não renderiza nada
  if (!active || !payload || payload.length === 0) return null;

  return (
    <div className="rounded-lg border border-border bg-card p-3 shadow-md text-sm">
      {/* Data do ponto no eixo X */}
      <p className="font-semibold text-foreground mb-1.5">{label}</p>

      {/* Uma linha por cada série (peso / % gordura) */}
      {payload.map((entry) => (
        <p key={entry.name} style={{ color: entry.color }} className="text-xs">
          {entry.name}: <span className="font-medium">{entry.value}</span>
          {/* Adiciona a unidade certa conforme o nome da série */}
          {entry.name === 'Peso (kg)' ? ' kg' : '%'}
        </p>
      ))}
    </div>
  );
}

// =============================================================================
// Componente principal
// =============================================================================

export default function AssessmentPage() {
  const [clients, setClients] = useState([]);
  const [selectedClientId, setSelectedClientId] = useState(null);

  const [assessments, setAssessments] = useState([]);
  const [checkins, setCheckins] = useState([]);
  const [loadingData, setLoadingData] = useState(false);

  const [activeTab, setActiveTab] = useState('assessments');

  // Estado do formulário de avaliação (criar ou editar)
  const [assessmentDialogOpen, setAssessmentDialogOpen] = useState(false);
  const [editingAssessment, setEditingAssessment] = useState(null);
  const [questionnaireOpen, setQuestionnaireOpen] = useState(false);

  // Estado do painel de notas do check-in
  const [notesDialogOpen, setNotesDialogOpen] = useState(false);
  const [notesTarget, setNotesTarget] = useState(null); // Check in alvo das notas

  // Check in a detalhar
  const [expandedCheckinId, setExpandedCheckinId] = useState(null);

  // Avaliação a detalhar
  const [expandedAssessmentId, setExpandedAssessmentId] = useState(null);

  // Confirmação de skip
  const [skipTarget, setSkipTarget] = useState(null);

  // Data alvo do check-in
  const [checkinTargetDate, setCheckinTargetDate] = useState(null);

  // Controla a visibilidade do mini-dialog de criação de check-in com data personalizada
  const [showCheckinDatePicker, setShowCheckinDatePicker] = useState(false);

  // Formulário de avaliação
  const {
    register,
    handleSubmit,
    reset,
    formState: { isSubmitting },
  } = useForm();

  // Formulário de notas do check-in
  const {
    register: registerNotes,
    handleSubmit: handleSubmitNotes,
    reset: resetNotes,
    formState: { isSubmitting: isSubmittingNotes },
  } = useForm();

  // Carregar clientes ao montar
  useEffect(() => {
    const load = async () => {
      try {
        const data = await getClients({ archived: false });
        // getClients pode retornar { items: [...] } ou diretamente [...]
        const list = Array.isArray(data) ? data : (data.items ?? []);
        setClients(list);
      } catch {
        toast.error('Erro ao carregar lista de clientes');
      }
    };
    load();
  }, []);

  // Carregar dados quando o cliente selecionado mudar
  useEffect(() => {
    if (!selectedClientId) {
      setAssessments([]);
      setCheckins([]);
      return;
    }

    const load = async () => {
      setLoadingData(true);
      try {
        // Promise.allSettled para carregar avaliações e check-ins em paralelo, sem falhar tudo se um der erro
        const [assessRes, checkinsRes] = await Promise.allSettled([
          getAssessmentsByClient(selectedClientId),
          getCheckinsByClient(selectedClientId),
        ]);

        if (assessRes.status === 'fulfilled') setAssessments(assessRes.value);
        else toast.error('Erro ao carregar avaliações do cliente');

        if (checkinsRes.status === 'fulfilled') setCheckins(checkinsRes.value);
        else toast.error('Erro ao carregar check-ins do cliente');
      } finally {
        setLoadingData(false);
      }
    };
    load();
  }, [selectedClientId]);

  // Dados derivados para o gráfico de progresso
  const progressData = useMemo(() => {
    const assessmentPoints = assessments
      .filter((a) => a.weight_kg != null || a.body_fat != null)
      .map((a) => ({
        source: 'assessment',
        timestamp: a.updated_at || a.created_at,
        date: formatDate(a.updated_at || a.created_at),
        weight: a.weight_kg ?? undefined,
        fat: a.body_fat ?? undefined,
      }));

    const checkinPoints = checkins
      .filter(
        (c) =>
          c.status === 'completed' &&
          (c.weight_kg != null || c.body_fat != null)
      )
      .map((c) => ({
        source: 'checkin',
        timestamp: c.completed_at || c.updated_at || c.requested_at,
        date: formatDate(c.completed_at || c.updated_at || c.requested_at),
        weight: c.weight_kg ?? undefined,
        fat: c.body_fat ?? undefined,
      }));

    return [...assessmentPoints, ...checkinPoints].sort(
      (a, b) => new Date(a.timestamp) - new Date(b.timestamp)
    );
  }, [assessments, checkins]);

  // Helpers

  const selectedClient = useMemo(
    () => clients.find((c) => c.id === selectedClientId),
    [clients, selectedClientId]
  );

  const openCreateAssessment = () => {
    setEditingAssessment(null);
    setQuestionnaireOpen(false);
    reset({
      weight_kg: '',
      height_cm: '',
      body_fat: '',
      notes: '',
      // Campos do questionário — todos vazios inicialmente
      occupation: '',
      activity_level: '',
      medical_conditions: '',
      medications: '',
      injuries: '',
      surgeries: '',
      family_history: '',
      sleep_hours: '',
      sleep_quality: '',
      stress_level: '',
      smoking: '',
      alcohol: '',
      water_intake_l: '',
      previous_training: '',
      sports_practiced: '',
      training_frequency: '',
      goals: '',
      dietary_restrictions: '',
      meal_frequency: '',
      supplements_current: '',
      preferred_schedule: '',
      gym_access: '',
      equipment_available: '',
      limitations: '',
    });
    setAssessmentDialogOpen(true);
  };

  const openEditAssessment = (assessment) => {
    setEditingAssessment(assessment);
    const q = assessment.health_questionnaire || {};
    reset({
      weight_kg: assessment.weight_kg ?? '',
      height_cm: assessment.height_cm ?? '',
      body_fat: assessment.body_fat ?? '',
      notes: assessment.notes ?? '',
      // Questionário — pré-preenchido com dados existentes
      occupation: q.occupation ?? '',
      activity_level: q.activity_level ?? '',
      medical_conditions: q.medical_conditions ?? '',
      medications: q.medications ?? '',
      injuries: q.injuries ?? '',
      surgeries: q.surgeries ?? '',
      family_history: q.family_history ?? '',
      sleep_hours: q.sleep_hours ?? '',
      sleep_quality: q.sleep_quality ?? '',
      stress_level: q.stress_level ?? '',
      smoking: q.smoking ?? '',
      alcohol: q.alcohol ?? '',
      water_intake_l: q.water_intake_l ?? '',
      previous_training: q.previous_training ?? '',
      sports_practiced: q.sports_practiced ?? '',
      training_frequency: q.training_frequency ?? '',
      goals: q.goals ?? '',
      dietary_restrictions: q.dietary_restrictions ?? '',
      meal_frequency: q.meal_frequency ?? '',
      supplements_current: q.supplements_current ?? '',
      preferred_schedule: q.preferred_schedule ?? '',
      gym_access: q.gym_access ?? '',
      equipment_available: q.equipment_available ?? '',
      limitations: q.limitations ?? '',
    });
    setQuestionnaireOpen(false);
    setAssessmentDialogOpen(true);
  };

  // Submit de avaliação (criação ou edição)
  const onSubmitAssessment = async (data) => {
    // Constrói o objeto de questionário apenas com campos preenchidos
    // Campos vazios são excluídos para não poluir o JSONB
    const qFields = [
      'occupation',
      'activity_level',
      'medical_conditions',
      'medications',
      'injuries',
      'surgeries',
      'family_history',
      'sleep_hours',
      'sleep_quality',
      'stress_level',
      'smoking',
      'alcohol',
      'water_intake_l',
      'previous_training',
      'sports_practiced',
      'training_frequency',
      'goals',
      'dietary_restrictions',
      'meal_frequency',
      'supplements_current',
      'preferred_schedule',
      'gym_access',
      'equipment_available',
      'limitations',
    ];

    const questionnaire = {};
    for (const key of qFields) {
      const val = data[key];
      if (val !== '' && val != null) {
        // Campos numéricos precisam de parse
        if (['sleep_hours', 'water_intake_l'].includes(key)) {
          const parsed = parseNullableFloat(val);
          if (parsed !== null) questionnaire[key] = parsed;
        } else if (
          ['stress_level', 'training_frequency', 'meal_frequency'].includes(key)
        ) {
          const parsed = parseNullableInt(val);
          if (parsed !== null) questionnaire[key] = parsed;
        } else {
          questionnaire[key] = val;
        }
      }
    }

    const payload = {
      client_id: selectedClientId,
      weight_kg: parseNullableFloat(data.weight_kg),
      height_cm: parseNullableFloat(data.height_cm),
      body_fat: parseNullableFloat(data.body_fat),
      notes: data.notes?.trim() || null,
      health_questionnaire:
        Object.keys(questionnaire).length > 0 ? questionnaire : null,
    };

    try {
      if (editingAssessment) {
        // No patch náo se envia client_id
        const { client_id, ...updatePayload } = payload;
        await updateAssessment(editingAssessment.id, updatePayload);
        toast.success('Avaliação atualizada');
      } else {
        await createAssessment(payload);
        toast.success('Avaliação criada');
      }
      setAssessmentDialogOpen(false);
      // Recarrega avaliações após criação/atualização
      const updated = await getAssessmentsByClient(selectedClientId);
      setAssessments(updated);
    } catch (error) {
      toast.error(error.response?.data?.detail || 'Erro ao guardar avaliação');
    }
  };

  const handleOpenCheckinDialog = () => {
    setCheckinTargetDate('');
    setShowCheckinDatePicker(true);
  };

  // Check in a criar pedido
  const handleCreateCheckin = async () => {
    try {
      const targetDate = checkinTargetDate || null;
      await createCheckin(selectedClientId, targetDate);
      const msg = targetDate
        ? `Pedido de check-in enviado para ${formatDate(targetDate)}`
        : 'Pedido de check-in enviado ao cliente';

      toast.success(msg);
      setShowCheckinDatePicker(false);
      setCheckinTargetDate('');
      const updated = await getCheckinsByClient(selectedClientId);
      setCheckins(updated);
    } catch (error) {
      toast.error(
        error.response?.data?.detail || 'Erro ao criar pedido de check-in'
      );
    }
  };

  // Check in a ignorar
  const handleSkip = async () => {
    if (!skipTarget) return;
    try {
      await skipCheckin(skipTarget.id);
      toast.success('Check-in ignorado');
      setSkipTarget(null);
      const updated = await getCheckinsByClient(selectedClientId);
      setCheckins(updated);
    } catch (error) {
      toast.error(error.response?.data?.detail || 'Erro ao ignorar check-in');
    }
  };

  // Check in a guardar notas
  const onSubmitNotes = async (data) => {
    try {
      await addCheckinNotes(notesTarget.id, data.trainer_notes);
      toast.success('Notas adicionadas ao check-in');
      setNotesTarget(null);
      resetNotes();
      const updated = await getCheckinsByClient(selectedClientId);
      setCheckins(updated);
    } catch (error) {
      toast.error(
        error.response?.data?.detail || 'Erro ao adicionar notas ao check-in'
      );
    }
  };

  const openNotesDialog = (checkin) => {
    setNotesTarget(checkin);
    resetNotes({ trainer_notes: checkin.trainer_notes || '' });
    setNotesDialogOpen(true);
  };

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* ── Cabeçalho ── */}
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Avaliações</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Avaliações iniciais de saúde e check-ins periódicos de progresso.
        </p>
      </div>

      {/* ── Selector de cliente ── */}
      <div className="flex flex-col gap-1.5 max-w-sm">
        <Label htmlFor="client-select">Cliente</Label>
        <Select value={selectedClientId} onValueChange={setSelectedClientId}>
          <SelectTrigger
            id="client-select"
            className="bg-background border-input"
          >
            <SelectValue placeholder="Selecciona um cliente..." />
          </SelectTrigger>
          <SelectContent>
            {clients.map((client) => (
              <SelectItem key={client.id} value={client.id}>
                {client.full_name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* ── Conteúdo principal — só aparece se houver cliente seleccionado ── */}
      {!selectedClientId ? (
        <div className="flex flex-col items-center justify-center py-20 text-center text-muted-foreground">
          <User className="h-12 w-12 mb-4 opacity-20" />
          <p className="font-medium text-base">
            Selecciona um cliente para ver as avaliações
          </p>
          <p className="text-sm mt-1">
            Escolhe um cliente no selector acima para começar.
          </p>
        </div>
      ) : (
        <Tabs value={activeTab} onValueChange={setActiveTab}>
          <TabsList className="bg-secondary">
            <TabsTrigger
              value="assessments"
              className="data-[state=active]:bg-background"
            >
              <ClipboardList className="h-4 w-4 mr-1.5" />
              Avaliações Iniciais
              {assessments.length > 0 && (
                <Badge variant="secondary" className="ml-1.5 text-xs h-5">
                  {assessments.length}
                </Badge>
              )}
            </TabsTrigger>
            <TabsTrigger
              value="checkins"
              className="data-[state=active]:bg-background"
            >
              <Activity className="h-4 w-4 mr-1.5" />
              Check-ins
              {checkins.filter((c) => c.status === 'pending').length > 0 && (
                <Badge className="ml-1.5 text-xs h-5 bg-yellow-500 text-white">
                  {checkins.filter((c) => c.status === 'pending').length}
                </Badge>
              )}
            </TabsTrigger>
            {/* TAB: PROGRESSO — gráfico de evolução de peso e % gordura */}
            {progressData.length > 0 && (
              <TabsTrigger
                value="progress"
                className="data-[state=active]:bg-background"
              >
                <TrendingUp className="h-4 w-4 mr-1.5" />
                Progresso
              </TabsTrigger>
            )}
          </TabsList>

          {/* ═══════════════════════════════════════════════════════════════════
                        TAB: AVALIAÇÕES INICIAIS
                    ═══════════════════════════════════════════════════════════════════ */}
          <TabsContent value="assessments" className="mt-4">
            <div className="flex justify-between items-center mb-4">
              <p className="text-sm text-muted-foreground">
                {assessments.length === 0
                  ? 'Sem avaliações registadas para este cliente.'
                  : `${assessments.length} avaliação${assessments.length !== 1 ? 'ões' : ''} registada${assessments.length !== 1 ? 's' : ''}.`}
              </p>
              <Button
                onClick={openCreateAssessment}
                size="sm"
                className="bg-primary text-primary-foreground hover:bg-primary/90"
              >
                <Plus className="h-4 w-4 mr-1.5" />
                Nova Avaliação
              </Button>
            </div>

            {loadingData ? (
              <div className="space-y-3">
                {[1, 2].map((i) => (
                  <div
                    key={i}
                    className="h-24 rounded-lg border border-border animate-pulse bg-card"
                  />
                ))}
              </div>
            ) : assessments.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-12 text-muted-foreground text-center">
                <ClipboardList className="h-10 w-10 mb-3 opacity-20" />
                <p className="font-medium">Nenhuma avaliação inicial</p>
                <p className="text-sm mt-1">
                  Cria a primeira avaliação para {selectedClient?.full_name}.
                </p>
              </div>
            ) : (
              <div className="space-y-3">
                {assessments.map((assessment) => {
                  const isExpanded = expandedAssessmentId === assessment.id;
                  const q = assessment.health_questionnaire || {};
                  const hasQuestionnaire = Object.keys(q).length > 0;

                  return (
                    <Card key={assessment.id} className="border-border">
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div>
                            <CardTitle className="text-base font-semibold">
                              Avaliação de {formatDate(assessment.created_at)}
                            </CardTitle>
                            <CardDescription>
                              Atualizada em {formatDate(assessment.updated_at)}
                            </CardDescription>
                          </div>
                          <div className="flex gap-2">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => openEditAssessment(assessment)}
                              className="text-muted-foreground hover:text-foreground"
                            >
                              <Edit className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() =>
                                setExpandedAssessmentId(
                                  isExpanded ? null : assessment.id
                                )
                              }
                              className="text-muted-foreground hover:text-foreground"
                            >
                              {isExpanded ? (
                                <ChevronDown className="h-4 w-4" />
                              ) : (
                                <ChevronRight className="h-4 w-4" />
                              )}
                            </Button>
                          </div>
                        </div>
                      </CardHeader>

                      <CardContent className="pt-0">
                        {/* Biometria — sempre visível no card */}
                        <div className="grid grid-cols-3 gap-3 mb-3">
                          <div className="flex flex-col items-center p-2 rounded-md bg-secondary/50">
                            <Scale className="h-4 w-4 text-muted-foreground mb-1" />
                            <span className="text-sm font-semibold">
                              {assessment.weight_kg != null
                                ? `${assessment.weight_kg} kg`
                                : '—'}
                            </span>
                            <span className="text-xs text-muted-foreground">
                              Peso
                            </span>
                          </div>
                          <div className="flex flex-col items-center p-2 rounded-md bg-secondary/50">
                            <span className="text-sm font-semibold">
                              {assessment.height_cm != null
                                ? `${assessment.height_cm} cm`
                                : '—'}
                            </span>
                            <span className="text-xs text-muted-foreground">
                              Altura
                            </span>
                          </div>
                          <div className="flex flex-col items-center p-2 rounded-md bg-secondary/50">
                            <span className="text-sm font-semibold">
                              {assessment.body_fat != null
                                ? `${assessment.body_fat}%`
                                : '—'}
                            </span>
                            <span className="text-xs text-muted-foreground">
                              % Gordura
                            </span>
                          </div>
                        </div>

                        {/* Notas — sempre visíveis */}
                        {assessment.notes && (
                          <p className="text-sm text-muted-foreground italic mb-3">
                            {assessment.notes}
                          </p>
                        )}

                        {/* Questionário — secção expansível */}
                        {isExpanded && hasQuestionnaire && (
                          <div className="border-t border-border pt-3 mt-1 space-y-3">
                            <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">
                              Questionário de Saúde
                            </p>

                            {/* Renderiza secções do questionário que têm dados */}
                            {[
                              {
                                title: 'Identificação',
                                fields: [
                                  { label: 'Profissão', key: 'occupation' },
                                  {
                                    label: 'Nível de actividade',
                                    key: 'activity_level',
                                  },
                                ],
                              },
                              {
                                title: 'Historial de Saúde',
                                fields: [
                                  {
                                    label: 'Condições médicas',
                                    key: 'medical_conditions',
                                  },
                                  { label: 'Medicação', key: 'medications' },
                                  { label: 'Lesões', key: 'injuries' },
                                  { label: 'Cirurgias', key: 'surgeries' },
                                  {
                                    label: 'Historial familiar',
                                    key: 'family_history',
                                  },
                                ],
                              },
                              {
                                title: 'Hábitos de Vida',
                                fields: [
                                  {
                                    label: 'Horas de sono',
                                    key: 'sleep_hours',
                                  },
                                  {
                                    label: 'Qualidade do sono',
                                    key: 'sleep_quality',
                                  },
                                  {
                                    label: 'Nível de stress',
                                    key: 'stress_level',
                                  },
                                  { label: 'Fumador', key: 'smoking' },
                                  { label: 'Álcool', key: 'alcohol' },
                                  {
                                    label: 'Água (L/dia)',
                                    key: 'water_intake_l',
                                  },
                                ],
                              },
                              {
                                title: 'Historial Desportivo',
                                fields: [
                                  {
                                    label: 'Treino anterior',
                                    key: 'previous_training',
                                  },
                                  {
                                    label: 'Desportos',
                                    key: 'sports_practiced',
                                  },
                                  {
                                    label: 'Frequência (dias/sem)',
                                    key: 'training_frequency',
                                  },
                                ],
                              },
                              {
                                title: 'Objetivos',
                                fields: [{ label: 'Objetivos', key: 'goals' }],
                              },
                              {
                                title: 'Alimentação',
                                fields: [
                                  {
                                    label: 'Restrições alimentares',
                                    key: 'dietary_restrictions',
                                  },
                                  {
                                    label: 'Refeições/dia',
                                    key: 'meal_frequency',
                                  },
                                  {
                                    label: 'Suplementação actual',
                                    key: 'supplements_current',
                                  },
                                ],
                              },
                              {
                                title: 'Preferências de Treino',
                                fields: [
                                  {
                                    label: 'Horário preferido',
                                    key: 'preferred_schedule',
                                  },
                                  {
                                    label: 'Acesso a ginásio',
                                    key: 'gym_access',
                                  },
                                  {
                                    label: 'Equipamento disponível',
                                    key: 'equipment_available',
                                  },
                                  {
                                    label: 'Limitações físicas',
                                    key: 'limitations',
                                  },
                                ],
                              },
                            ].map((section) => {
                              // Só mostra secções com pelo menos um campo preenchido
                              const filledFields = section.fields.filter(
                                (f) => q[f.key] != null && q[f.key] !== ''
                              );
                              if (filledFields.length === 0) return null;
                              return (
                                <div key={section.title}>
                                  <p className="text-xs font-medium text-foreground mb-1.5">
                                    {section.title}
                                  </p>
                                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
                                    {filledFields.map((f) => (
                                      <div key={f.key} className="flex gap-1.5">
                                        <span className="text-xs text-muted-foreground min-w-30">
                                          {f.label}:
                                        </span>
                                        <span className="text-xs text-foreground">
                                          {String(q[f.key])}
                                        </span>
                                      </div>
                                    ))}
                                  </div>
                                </div>
                              );
                            })}
                          </div>
                        )}

                        {/* Botão "ver mais" apenas se houver questionário */}
                        {hasQuestionnaire && (
                          <button
                            type="button"
                            onClick={() =>
                              setExpandedAssessmentId(
                                isExpanded ? null : assessment.id
                              )
                            }
                            className="text-xs text-primary hover:underline mt-2"
                          >
                            {isExpanded
                              ? 'Ocultar questionário'
                              : 'Ver questionário de saúde'}
                          </button>
                        )}
                      </CardContent>
                    </Card>
                  );
                })}
              </div>
            )}
          </TabsContent>

          {/* ═══════════════════════════════════════════════════════════════════
                        TAB: CHECK-INS
                    ═══════════════════════════════════════════════════════════════════ */}
          <TabsContent value="checkins" className="mt-4">
            <div className="flex justify-between items-center mb-4">
              <p className="text-sm text-muted-foreground">
                {checkins.length === 0
                  ? 'Sem check-ins para este cliente.'
                  : `${checkins.length} check-in${checkins.length !== 1 ? 's' : ''} registado${checkins.length !== 1 ? 's' : ''}.`}
              </p>
              <Button
                onClick={handleOpenCheckinDialog}
                size="sm"
                className="bg-primary text-primary-foreground hover:bg-primary/90"
              >
                <Plus className="h-4 w-4 mr-1.5" />
                Pedir Check-in
              </Button>
            </div>

            {loadingData ? (
              <div className="space-y-3">
                {[1, 2, 3].map((i) => (
                  <div
                    key={i}
                    className="h-20 rounded-lg border border-border animate-pulse bg-card"
                  />
                ))}
              </div>
            ) : checkins.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-12 text-muted-foreground text-center">
                <Activity className="h-10 w-10 mb-3 opacity-20" />
                <p className="font-medium">Nenhum check-in</p>
                <p className="text-sm mt-1">
                  Pede um check-in a {selectedClient?.full_name} para acompanhar
                  o progresso.
                </p>
              </div>
            ) : (
              <div className="space-y-3">
                {checkins.map((checkin) => {
                  const isExpanded = expandedCheckinId === checkin.id;
                  const hasResponse = checkin.status === 'completed';
                  const q = checkin.questionnaire || {};

                  return (
                    <Card key={checkin.id} className="border-border">
                      <CardHeader className="pb-2">
                        <div className="flex items-start justify-between gap-3">
                          <div className="flex items-center gap-2 flex-wrap">
                            <CheckinStatusBadge status={checkin.status} />
                            <span className="text-sm text-muted-foreground">
                              Pedido em {formatDate(checkin.requested_at)}
                            </span>
                            {checkin.target_date && (
                              <span className="text-xs px-2 py-0.5 rounded-full bg-primary/10 text-primary font-medium">
                                Para {formatDate(checkin.target_date)}
                              </span>
                            )}
                            {checkin.completed_at && (
                              <span className="text-sm text-muted-foreground">
                                · Respondido em{' '}
                                {formatDate(checkin.completed_at)}
                              </span>
                            )}
                          </div>

                          {/* Acções — dependem do estado */}
                          <div className="flex gap-1 shrink-0">
                            {hasResponse && (
                              <>
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() => openNotesDialog(checkin)}
                                  className="text-muted-foreground hover:text-foreground text-xs"
                                >
                                  <MessageSquare className="h-3.5 w-3.5 mr-1" />
                                  Notas
                                </Button>
                                <Button
                                  variant="ghost"
                                  size="sm"
                                  onClick={() =>
                                    setExpandedCheckinId(
                                      isExpanded ? null : checkin.id
                                    )
                                  }
                                  className="text-muted-foreground hover:text-foreground"
                                >
                                  {isExpanded ? (
                                    <ChevronDown className="h-4 w-4" />
                                  ) : (
                                    <ChevronRight className="h-4 w-4" />
                                  )}
                                </Button>
                              </>
                            )}
                            {checkin.status === 'pending' && (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setSkipTarget(checkin)}
                                className="text-muted-foreground hover:text-destructive text-xs"
                              >
                                <SkipForward className="h-3.5 w-3.5 mr-1" />
                                Ignorar
                              </Button>
                            )}
                          </div>
                        </div>
                      </CardHeader>

                      {/* Detalhes da resposta — apenas quando expandido e completo */}
                      {isExpanded && hasResponse && (
                        <CardContent className="pt-0">
                          <div className="border-t border-border pt-3 space-y-4">
                            {/* Biometria reportada pelo cliente */}
                            {(checkin.weight_kg != null ||
                              checkin.body_fat != null) && (
                              <div>
                                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-2">
                                  Biometria
                                </p>
                                <div className="flex gap-4">
                                  {checkin.weight_kg != null && (
                                    <div className="flex flex-col items-center p-2 rounded-md bg-secondary/50 min-w-20">
                                      <span className="text-sm font-semibold">
                                        {checkin.weight_kg} kg
                                      </span>
                                      <span className="text-xs text-muted-foreground">
                                        Peso
                                      </span>
                                    </div>
                                  )}
                                  {checkin.body_fat != null && (
                                    <div className="flex flex-col items-center p-2 rounded-md bg-secondary/50 min-w-20">
                                      <span className="text-sm font-semibold">
                                        {checkin.body_fat}%
                                      </span>
                                      <span className="text-xs text-muted-foreground">
                                        % Gordura
                                      </span>
                                    </div>
                                  )}
                                </div>
                              </div>
                            )}

                            {/* Questionário periódico */}
                            {Object.keys(q).length > 0 && (
                              <div>
                                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-2">
                                  Questionário
                                </p>
                                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                                  {q.energy_level != null && (
                                    <div className="flex justify-between text-sm">
                                      <span className="text-muted-foreground">
                                        Energia:
                                      </span>
                                      <ScaleValue value={q.energy_level} />
                                    </div>
                                  )}
                                  {q.recovery_quality != null && (
                                    <div className="flex justify-between text-sm">
                                      <span className="text-muted-foreground">
                                        Recuperação:
                                      </span>
                                      <ScaleValue value={q.recovery_quality} />
                                    </div>
                                  )}
                                  {q.training_performance != null && (
                                    <div className="flex justify-between text-sm">
                                      <span className="text-muted-foreground">
                                        Performance:
                                      </span>
                                      <ScaleValue
                                        value={q.training_performance}
                                      />
                                    </div>
                                  )}
                                  {q.stress_level != null && (
                                    <div className="flex justify-between text-sm">
                                      <span className="text-muted-foreground">
                                        Stress:
                                      </span>
                                      <ScaleValue value={q.stress_level} />
                                    </div>
                                  )}
                                  {q.plan_adherence_pct != null && (
                                    <div className="flex justify-between text-sm">
                                      <span className="text-muted-foreground">
                                        Aderência ao plano:
                                      </span>
                                      <span className="font-medium">
                                        {q.plan_adherence_pct}%
                                      </span>
                                    </div>
                                  )}
                                  {q.daily_water_intake_l != null && (
                                    <div className="flex justify-between text-sm">
                                      <span className="text-muted-foreground">
                                        Água (L/dia):
                                      </span>
                                      <span className="font-medium">
                                        {q.daily_water_intake_l}L
                                      </span>
                                    </div>
                                  )}
                                </div>
                                {q.body_response && (
                                  <div className="mt-2 text-sm">
                                    <span className="text-muted-foreground">
                                      Resposta corporal:{' '}
                                    </span>
                                    {q.body_response}
                                  </div>
                                )}
                                {q.injuries && (
                                  <div className="mt-1 text-sm">
                                    <span className="text-muted-foreground">
                                      Lesões/dores:{' '}
                                    </span>
                                    {q.injuries}
                                  </div>
                                )}
                              </div>
                            )}

                            {/* Notas do cliente */}
                            {checkin.client_notes && (
                              <div>
                                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">
                                  Notas do Cliente
                                </p>
                                <p className="text-sm italic">
                                  {checkin.client_notes}
                                </p>
                              </div>
                            )}

                            {/* Notas do Personal Trainer */}
                            {checkin.trainer_notes && (
                              <div>
                                <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-1">
                                  As Minhas Notas
                                </p>
                                <p className="text-sm">
                                  {checkin.trainer_notes}
                                </p>
                              </div>
                            )}
                          </div>
                        </CardContent>
                      )}
                    </Card>
                  );
                })}
              </div>
            )}
          </TabsContent>

          {/* ═══════════════════════════════════════════════════════════════════
              [P7-03] NOVO TAB: PROGRESSO — gráficos de evolução biométrica.
              
              Este tab só renderiza se progressData.length > 0 (verificado
              também no TabsTrigger). Se o cliente nunca respondeu a um check-in
              com dados biométricos, o tab não aparece de todo.
              ═══════════════════════════════════════════════════════════════════ */}
          <TabsContent value="progress" className="mt-4">
            <div className="space-y-6">
              {/* ── Card: Peso ao longo do tempo ── */}
              {/* Só renderiza este card se houver pelo menos um ponto com peso */}
              {progressData.some((d) => d.weight !== undefined) && (
                <Card className="border-border">
                  <CardHeader className="pb-2">
                    <CardTitle className="text-base font-semibold flex items-center gap-2">
                      <Scale className="h-4 w-4 text-muted-foreground" />
                      Peso Corporal (kg)
                    </CardTitle>
                    <CardDescription>
                      Evolução registada nos check-ins respondidos.
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    {/*
                      ResponsiveContainer — envolve sempre o gráfico Recharts.
                      height fixo em px, width="100%" ajusta-se ao contentor pai.
                      Sem este wrapper, o gráfico tem dimensão 0 ou transborda.
                    */}
                    <ResponsiveContainer width="100%" height={220}>
                      <LineChart
                        data={progressData}
                        margin={{ top: 5, right: 20, left: 0, bottom: 5 }}
                      >
                        {/*
                          CartesianGrid — linhas de grelha de fundo.
                          strokeDasharray="3 3" cria traços, não linhas sólidas.
                          A cor usa a variável CSS --border do tema shadcn.
                        */}
                        <CartesianGrid
                          strokeDasharray="3 3"
                          stroke="hsl(var(--border))"
                          opacity={0.5}
                        />

                        {/*
                          XAxis — eixo horizontal com as datas.
                          dataKey="date" referencia o campo do objeto de dados.
                          tick={{ fontSize: 11 }} reduz o tamanho do texto
                          para caber datas em espaço reduzido.
                        */}
                        <XAxis
                          dataKey="date"
                          tick={{
                            fontSize: 11,
                            fill: 'hsl(var(--muted-foreground))',
                          }}
                          axisLine={{ stroke: 'hsl(var(--border))' }}
                          tickLine={false}
                        />

                        {/*
                          YAxis — eixo vertical com os valores.
                          domain={['auto', 'auto']} ajusta a escala ao intervalo
                          real dos dados, evitando que o gráfico comece sempre em 0
                          (uma variação de 2 kg pareceria plana se o eixo fosse 0-100).
                        */}
                        <YAxis
                          domain={['auto', 'auto']}
                          tick={{
                            fontSize: 11,
                            fill: 'hsl(var(--muted-foreground))',
                          }}
                          axisLine={{ stroke: 'hsl(var(--border))' }}
                          tickLine={false}
                          unit=" kg"
                        />

                        {/* Tooltip personalizado — ver componente ProgressChartTooltip acima */}
                        <Tooltip content={<ProgressChartTooltip />} />

                        {/*
                          Line — a série de dados.
                          type="monotone" suaviza a linha (curva de Bezier)
                          em vez de segmentos retos, tornando a tendência mais legível.
                          dot={{ r: 4 }} coloca um círculo em cada ponto de dados.
                          connectNulls={false} deixa a linha quebrada se faltar um valor —
                          comportamento correto aqui porque undefined representa
                          check-ins sem peso registado.
                        */}
                        <Line
                          type="monotone"
                          dataKey="weight"
                          name="Peso (kg)"
                          stroke="hsl(var(--primary))"
                          strokeWidth={2}
                          dot={{ r: 4, fill: 'hsl(var(--primary))' }}
                          activeDot={{ r: 6 }}
                          connectNulls={false}
                        />
                      </LineChart>
                    </ResponsiveContainer>
                  </CardContent>
                </Card>
              )}

              {/* ── Card: % Gordura ao longo do tempo ── */}
              {/* Só renderiza se houver pelo menos um ponto com gordura */}
              {progressData.some((d) => d.fat !== undefined) && (
                <Card className="border-border">
                  <CardHeader className="pb-2">
                    <CardTitle className="text-base font-semibold flex items-center gap-2">
                      <TrendingUp className="h-4 w-4 text-muted-foreground" />
                      Percentagem de Gordura Corporal (%)
                    </CardTitle>
                    <CardDescription>
                      Evolução registada nos check-ins respondidos.
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <ResponsiveContainer width="100%" height={220}>
                      <LineChart
                        data={progressData}
                        margin={{ top: 5, right: 20, left: 0, bottom: 5 }}
                      >
                        <CartesianGrid
                          strokeDasharray="3 3"
                          stroke="hsl(var(--border))"
                          opacity={0.5}
                        />
                        <XAxis
                          dataKey="date"
                          tick={{
                            fontSize: 11,
                            fill: 'hsl(var(--muted-foreground))',
                          }}
                          axisLine={{ stroke: 'hsl(var(--border))' }}
                          tickLine={false}
                        />
                        <YAxis
                          domain={['auto', 'auto']}
                          tick={{
                            fontSize: 11,
                            fill: 'hsl(var(--muted-foreground))',
                          }}
                          axisLine={{ stroke: 'hsl(var(--border))' }}
                          tickLine={false}
                          unit="%"
                        />
                        <Tooltip content={<ProgressChartTooltip />} />
                        {/*
                          Cor diferente do peso (#f97316 = orange-500) para
                          distinguir visualmente as duas métricas sem depender
                          de uma legenda — o título do card já identifica a métrica.
                        */}
                        <Line
                          type="monotone"
                          dataKey="fat"
                          name="Gordura (%)"
                          stroke="#f97316"
                          strokeWidth={2}
                          dot={{ r: 4, fill: '#f97316' }}
                          activeDot={{ r: 6 }}
                          connectNulls={false}
                        />
                      </LineChart>
                    </ResponsiveContainer>
                  </CardContent>
                </Card>
              )}

              {/* ── Nota informativa no rodapé ── */}
              <p className="text-xs text-muted-foreground text-center pb-2">
                Os gráficos incluem apenas check-ins respondidos com dados
                biométricos registados.
              </p>
            </div>
          </TabsContent>
        </Tabs>
      )}

      {/* ═══════════════════════════════════════════════════════════════════════
                    DIALOG: Criar / Editar Avaliação
                ═══════════════════════════════════════════════════════════════════════ */}
      <Dialog
        open={assessmentDialogOpen}
        onOpenChange={setAssessmentDialogOpen}
      >
        <DialogContent className="bg-card border-border text-foreground sm:max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {editingAssessment
                ? 'Editar Avaliação'
                : 'Nova Avaliação Inicial'}
            </DialogTitle>
            <DialogDescription className="text-muted-foreground">
              {editingAssessment
                ? `Editar a avaliação de ${formatDate(editingAssessment.created_at)}.`
                : `Nova avaliação para ${selectedClient?.full_name}.`}
            </DialogDescription>
          </DialogHeader>

          <form
            onSubmit={handleSubmit(onSubmitAssessment)}
            className="space-y-4 mt-2"
          >
            {/* Dados biométricos */}
            <div>
              <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide mb-3">
                Dados Biométricos
              </p>
              <div className="grid grid-cols-3 gap-3">
                <div className="space-y-1.5">
                  <Label htmlFor="weight_kg">Peso (kg)</Label>
                  <Input
                    id="weight_kg"
                    type="number"
                    step="0.1"
                    placeholder="Ex: 75"
                    className="bg-background border-input"
                    {...register('weight_kg')}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="height_cm">Altura (cm)</Label>
                  <Input
                    id="height_cm"
                    type="number"
                    placeholder="Ex: 175"
                    className="bg-background border-input"
                    {...register('height_cm')}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="body_fat">% Gordura</Label>
                  <Input
                    id="body_fat"
                    type="number"
                    step="0.1"
                    placeholder="Ex: 18"
                    className="bg-background border-input"
                    {...register('body_fat')}
                  />
                </div>
              </div>
            </div>

            {/* Notas gerais */}
            <div className="space-y-1.5">
              <Label htmlFor="notes">Notas Gerais</Label>
              <Textarea
                id="notes"
                rows={2}
                placeholder="Observações relevantes..."
                className="bg-background border-input"
                {...register('notes')}
              />
            </div>

            {/* Questionário de saúde — secção expansível para não sobrecarregar */}
            <Collapsible
              open={questionnaireOpen}
              onOpenChange={setQuestionnaireOpen}
            >
              <CollapsibleTrigger asChild>
                <button
                  type="button"
                  className="flex items-center gap-2 text-sm font-medium text-foreground hover:text-primary w-full py-2 border-t border-border"
                >
                  {questionnaireOpen ? (
                    <ChevronDown className="h-4 w-4" />
                  ) : (
                    <ChevronRight className="h-4 w-4" />
                  )}
                  Questionário de Saúde
                  <span className="text-xs text-muted-foreground font-normal ml-1">
                    (opcional — {questionnaireOpen ? 'ocultar' : 'expandir'})
                  </span>
                </button>
              </CollapsibleTrigger>
              <CollapsibleContent className="space-y-4 mt-2">
                {/* Secção 1 — Identificação */}
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-2">
                    Identificação
                  </p>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="occupation">
                        Profissão
                      </Label>
                      <Input
                        id="occupation"
                        placeholder="Ex: Escritório"
                        className="bg-background border-input text-sm h-8"
                        {...register('occupation')}
                      />
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="activity_level">
                        Nível de actividade
                      </Label>
                      <Input
                        id="activity_level"
                        placeholder="Ex: Sedentário"
                        className="bg-background border-input text-sm h-8"
                        {...register('activity_level')}
                      />
                    </div>
                  </div>
                </div>

                {/* Secção 2 — Historial de Saúde */}
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-2">
                    Historial de Saúde
                  </p>
                  <div className="space-y-2">
                    {[
                      {
                        id: 'medical_conditions',
                        label: 'Condições médicas',
                        ph: 'Diabetes, hipertensão...',
                      },
                      {
                        id: 'medications',
                        label: 'Medicação actual',
                        ph: 'Nome dos medicamentos...',
                      },
                      {
                        id: 'injuries',
                        label: 'Lesões',
                        ph: 'Lesões passadas ou actuais...',
                      },
                      {
                        id: 'surgeries',
                        label: 'Cirurgias',
                        ph: 'Cirurgias relevantes...',
                      },
                      {
                        id: 'family_history',
                        label: 'Historial familiar',
                        ph: 'Doenças hereditárias...',
                      },
                    ].map((f) => (
                      <div key={f.id} className="space-y-1">
                        <Label className="text-xs" htmlFor={f.id}>
                          {f.label}
                        </Label>
                        <Input
                          id={f.id}
                          placeholder={f.ph}
                          className="bg-background border-input text-sm h-8"
                          {...register(f.id)}
                        />
                      </div>
                    ))}
                  </div>
                </div>

                {/* Secção 3 — Hábitos de Vida */}
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-2">
                    Hábitos de Vida
                  </p>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="sleep_hours">
                        Horas de sono
                      </Label>
                      <Input
                        id="sleep_hours"
                        type="number"
                        step="0.5"
                        placeholder="Ex: 7"
                        className="bg-background border-input text-sm h-8"
                        {...register('sleep_hours')}
                      />
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="sleep_quality">
                        Qualidade do sono
                      </Label>
                      <Input
                        id="sleep_quality"
                        placeholder="Boa / Razoável / Má"
                        className="bg-background border-input text-sm h-8"
                        {...register('sleep_quality')}
                      />
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="stress_level">
                        Stress (1-5)
                      </Label>
                      <Input
                        id="stress_level"
                        type="number"
                        min="1"
                        max="5"
                        placeholder="1-5"
                        className="bg-background border-input text-sm h-8"
                        {...register('stress_level')}
                      />
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="alcohol">
                        Álcool
                      </Label>
                      <Input
                        id="alcohol"
                        placeholder="Nunca / Ocasional / Frequente"
                        className="bg-background border-input text-sm h-8"
                        {...register('alcohol')}
                      />
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="water_intake_l">
                        Água (L/dia)
                      </Label>
                      <Input
                        id="water_intake_l"
                        type="number"
                        step="0.1"
                        placeholder="Ex: 2"
                        className="bg-background border-input text-sm h-8"
                        {...register('water_intake_l')}
                      />
                    </div>
                  </div>
                </div>

                {/* Secção 4 — Historial Desportivo */}
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-2">
                    Historial Desportivo
                  </p>
                  <div className="space-y-2">
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="previous_training">
                        Treino anterior
                      </Label>
                      <Textarea
                        id="previous_training"
                        rows={2}
                        placeholder="Experiência de treino anterior..."
                        className="bg-background border-input text-sm"
                        {...register('previous_training')}
                      />
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <Label className="text-xs" htmlFor="sports_practiced">
                          Desportos praticados
                        </Label>
                        <Input
                          id="sports_practiced"
                          placeholder="Ex: Futebol, natação"
                          className="bg-background border-input text-sm h-8"
                          {...register('sports_practiced')}
                        />
                      </div>
                      <div className="space-y-1">
                        <Label className="text-xs" htmlFor="training_frequency">
                          Frequência (dias/sem)
                        </Label>
                        <Input
                          id="training_frequency"
                          type="number"
                          min="0"
                          max="7"
                          placeholder="0-7"
                          className="bg-background border-input text-sm h-8"
                          {...register('training_frequency')}
                        />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Secção 5 — Objetivos */}
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-2">
                    Objetivos
                  </p>
                  <div className="space-y-1">
                    <Label className="text-xs" htmlFor="goals">
                      Objetivos
                    </Label>
                    <Textarea
                      id="goals"
                      rows={2}
                      placeholder="Perda de peso, ganho muscular, saúde..."
                      className="bg-background border-input text-sm"
                      {...register('goals')}
                    />
                  </div>
                </div>

                {/* Secção 6 — Alimentação */}
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-2">
                    Alimentação
                  </p>
                  <div className="space-y-2">
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="dietary_restrictions">
                        Restrições alimentares
                      </Label>
                      <Input
                        id="dietary_restrictions"
                        placeholder="Vegetariano, sem glúten..."
                        className="bg-background border-input text-sm h-8"
                        {...register('dietary_restrictions')}
                      />
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <Label className="text-xs" htmlFor="meal_frequency">
                          Refeições/dia
                        </Label>
                        <Input
                          id="meal_frequency"
                          type="number"
                          min="1"
                          max="10"
                          placeholder="Ex: 4"
                          className="bg-background border-input text-sm h-8"
                          {...register('meal_frequency')}
                        />
                      </div>
                      <div className="space-y-1">
                        <Label
                          className="text-xs"
                          htmlFor="supplements_current"
                        >
                          Suplementação actual
                        </Label>
                        <Input
                          id="supplements_current"
                          placeholder="Ex: Whey, creatina"
                          className="bg-background border-input text-sm h-8"
                          {...register('supplements_current')}
                        />
                      </div>
                    </div>
                  </div>
                </div>

                {/* Secção 7 — Preferências de Treino */}
                <div>
                  <p className="text-xs font-medium text-muted-foreground mb-2">
                    Preferências de Treino
                  </p>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="preferred_schedule">
                        Horário preferido
                      </Label>
                      <Input
                        id="preferred_schedule"
                        placeholder="Manhã / Tarde / Noite"
                        className="bg-background border-input text-sm h-8"
                        {...register('preferred_schedule')}
                      />
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs" htmlFor="equipment_available">
                        Equipamento disponível
                      </Label>
                      <Input
                        id="equipment_available"
                        placeholder="Halteres, elásticos..."
                        className="bg-background border-input text-sm h-8"
                        {...register('equipment_available')}
                      />
                    </div>
                    <div className="col-span-2 space-y-1">
                      <Label className="text-xs" htmlFor="limitations">
                        Limitações físicas
                      </Label>
                      <Input
                        id="limitations"
                        placeholder="Ex: Joelho direito, lombar"
                        className="bg-background border-input text-sm h-8"
                        {...register('limitations')}
                      />
                    </div>
                  </div>
                </div>
              </CollapsibleContent>
            </Collapsible>

            {/* Botões */}
            <div className="flex justify-end gap-3 pt-2 border-t border-border">
              <Button
                type="button"
                variant="ghost"
                onClick={() => setAssessmentDialogOpen(false)}
                className="text-muted-foreground"
              >
                Cancelar
              </Button>
              <Button
                type="submit"
                disabled={isSubmitting}
                className="bg-primary text-primary-foreground hover:bg-primary/90"
              >
                {isSubmitting
                  ? 'A guardar...'
                  : editingAssessment
                    ? 'Guardar Alterações'
                    : 'Criar Avaliação'}
              </Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>

      {/* ═══════════════════════════════════════════════════════════════════════
                DIALOG: Notas do Personal Trainer num Check-in
            ═══════════════════════════════════════════════════════════════════════ */}
      <Dialog open={notesDialogOpen} onOpenChange={setNotesDialogOpen}>
        <DialogContent className="bg-card border-border text-foreground sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Notas do Personal Trainer</DialogTitle>
            <DialogDescription className="text-muted-foreground">
              As notas são internas — não ficam visíveis ao cliente.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3 mt-2">
            <Textarea
              rows={4}
              placeholder="Observações sobre o progresso, ajustes a fazer..."
              className="bg-background border-input"
              {...registerNotes('trainer_notes')}
            />
            <div className="flex justify-end gap-3">
              <Button
                variant="ghost"
                onClick={() => setNotesDialogOpen(false)}
                className="text-muted-foreground"
              >
                Cancelar
              </Button>
              <Button
                onClick={handleSubmitNotes(onSubmitNotes)}
                disabled={isSubmittingNotes}
                className="bg-primary text-primary-foreground hover:bg-primary/90"
              >
                {isSubmittingNotes ? 'A guardar...' : 'Guardar Notas'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* ═══════════════════════════════════════════════════════════════════════
                DIALOG: Definir data alvo do check-in (AS-01)
            ═══════════════════════════════════════════════════════════════════════ */}
      {/*
        AlertDialog em vez de Dialog porque a acção é simples (confirmar/cancelar)
        e não requer formulário complexo. O input de data é um campo nativo HTML5
        — não se usa react-hook-form porque é um único campo sem validação complexa.
 
        Razão de negócio: o Personal Trainer pode criar o check-in sem data alvo (clica
        "Enviar Sem Data") ou definir uma data específica. Separar este passo
        do botão principal torna a intenção mais explícita e evita erros.
      */}
      <AlertDialog
        open={showCheckinDatePicker}
        onOpenChange={(open) => !open && setShowCheckinDatePicker(false)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Pedir Check-in</AlertDialogTitle>
            <AlertDialogDescription>
              Define uma data alvo para o cliente responder (opcional). O
              cliente verá esta data na sua dashboard.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div className="py-2">
            <Label
              htmlFor="checkin-target-date"
              className="text-sm font-medium mb-2 block"
            >
              Data alvo (opcional)
            </Label>
            <input
              id="checkin-target-date"
              type="date"
              value={checkinTargetDate}
              onChange={(e) => setCheckinTargetDate(e.target.value)}
              min={new Date().toISOString().split('T')[0]}
              className="w-full h-9 rounded-md border border-input bg-background px-3 text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => setShowCheckinDatePicker(false)}>
              Cancelar
            </AlertDialogCancel>
            {/* Botão secundário: envia sem data — não fecha o AlertDialog, chama handleCreateCheckin */}
            <Button
              variant="outline"
              onClick={() => {
                setCheckinTargetDate('');
                handleCreateCheckin();
              }}
            >
              Enviar Sem Data
            </Button>
            <AlertDialogAction onClick={handleCreateCheckin}>
              Enviar Check-in
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* ═══════════════════════════════════════════════════════════════════════
                ALERT DIALOG: Confirmar ignorar check-in
            ═══════════════════════════════════════════════════════════════════════ */}
      <AlertDialog open={!!skipTarget} onOpenChange={() => setSkipTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Ignorar check-in?</AlertDialogTitle>
            <AlertDialogDescription>
              O check-in pedido em{' '}
              <strong>
                {skipTarget ? formatDate(skipTarget.requested_at) : ''}
              </strong>{' '}
              será marcado como ignorado. O cliente deixará de ver o pedido na
              sua dashboard.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancelar</AlertDialogCancel>
            <AlertDialogAction onClick={handleSkip}>Ignorar</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
