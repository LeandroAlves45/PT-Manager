/**
 * TrainingPlanEditorPage.jsx — Editor de plano de treino em página completa (TP-01)
 *
 * Rota: /trainer/planos/:id
 *
 * Substitui o Sheet lateral que existia em TrainingPlans.jsx.
 * A vantagem prática: dias e exercícios visíveis sem constrangimentos
 * de altura de painel — o Personal Trainer vê tudo sem scroll forçado.
 *
 * Responsabilidades:
 *   - Carregar o plano pelo ID da URL (useParams)
 *   - Carregar os dias e exercícios do plano
 *   - Passar dados ao PlanDaysList existente (sem o reescrever)
 *   - Mostrar cabeçalho com nome, status, botão "Ativar para cliente"
 *   - Botão de voltar → /trainer/planos
 */

import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { toast } from 'react-toastify';

import {
  getTrainingPlans,
  getPlanDays,
  getDayExercises,
  setClientActivePlan,
} from '@/api/trainingPlan';
import { useClients } from '@/hooks/useClients';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ArrowLeft, ClipboardList, Zap } from 'lucide-react';

import PlanDaysList from '@/components/training-plans/PlanDaysList';
import ActivatePlanDialog from '@/components/training-plans/ActivatePlanDialog';

// =============================================================================
// Helper: estilos do badge de status
// =============================================================================
function getStatusStyle(status) {
  switch (status) {
    case 'published':
      return {
        className: 'bg-green-500/50 text-green-500',
        label: 'Publicado',
      };
    case 'draft':
      return {
        className: 'bg-yellow-500/50 text-yellow-500',
        label: 'Rascunho',
      };
    case 'archived':
      return {
        className: 'border-muted text-muted-foreground',
        label: 'Arquivado',
      };
    default:
      return {
        className: 'border-border text-muted-foreground',
        label: status ?? '-',
      };
  }
}

export default function TrainingPlanEditorPage() {
  // useParams para pegar o ID do plano da URL
  const { id } = useParams();
  const navigate = useNavigate();

  const { clients } = useClients();

  // Estado do dialog de ativar plano para cliente
  const [plan, setPlan] = useState(null);
  const [days, setDays] = useState([]);
  const [loadingPlan, setLoadingPlan] = useState(true);
  const [loadingDays, setLoadingDays] = useState(false);

  // Estado do dialog de ativação
  const [activateOpen, setActivateOpen] = useState(false);

  // Carregar plano e dias ao montar o componente
  useEffect(() => {
    const load = async () => {
      setLoadingPlan(true);
      try {
        const all = await getTrainingPlans();
        // A API devolve uma lista
        const found = Array.isArray(all)
          ? all.find((p) => p.id === id)
          : all?.items?.find((p) => p.id === id);

        if (!found) {
          toast.error('Plano de treino não encontrado');
          navigate('/trainer/planos');
          return;
        }
        setPlan(found);
      } catch {
        toast.error('Erro ao carregar plano de treino');
        navigate('/trainer/planos');
      } finally {
        setLoadingPlan(false);
      }
    };
    load();
  }, [id, navigate]);

  // Carregar dias do plano
  useEffect(() => {
    if (!plan) return;
    loadDays();
  }, [plan?.id]);

  // loadDays busca os dias e os exercícios de cada dia
  const loadDays = async () => {
    setLoadingDays(true);
    try {
      const rawDays = await getPlanDays(plan.id);

      const daysWithExercises = await Promise.all(
        rawDays.map(async (day) => {
          try {
            const exercises = await getDayExercises(day.id);
            return { ...day, exercises };
          } catch {
            return { ...day, exercises: [] };
          }
        })
      );

      setDays(daysWithExercises);
    } catch {
      toast.error('Erro ao carregar dias do plano');
    } finally {
      setLoadingDays(false);
    }
  };

  // Handler passado ao PlanDaysList para recarregar os dias após edição
  const handleRefreshDays = () => loadDays();

  // Ativar plano para cliente
  const handleActivate = async ({ client_id, active_from }) => {
    try {
      await setClientActivePlan({
        client_id,
        training_plan_id: plan.id,
        active_from,
      });
      toast.success('Plano ativado para cliente com sucesso.');
      setActivateOpen(false);
    } catch (error) {
      toast.error(
        error.response?.data?.message || 'Erro ao ativar plano para cliente.'
      );
    }
  };

  // Render: Loading
  if (loadingPlan) {
    return (
      <div className="p-4 lg:p-6 space-y-4">
        {/* Skeleton do cabeçalho */}
        <div className="flex items-center gap-3">
          <div className="h-9 w-9 rounded-lg bg-muted animate-pulse" />
          <div className="h-6 w-48 rounded bg-muted animate-pulse" />
        </div>
        {/* Skeleton dos dias */}
        <div className="space-y-3 mt-4">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="h-14 rounded-lg border border-border bg-card animate-pulse"
            />
          ))}
        </div>
      </div>
    );
  }

  if (!plan) return null; // Plano não encontrado, mas já mostramos toast e redirecionamos

  const statusStyle = getStatusStyle(plan.status);

  // Render: página completa
  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      {/* ── Cabeçalho ── */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          {/* Botão "Voltar" — navega para /trainer/planos. */}
          <button
            type="button"
            onClick={() => navigate('/trainer/planos')}
            className="p-2 rounded-lg text-muted-foreground hover:text-foreground hover:bg-accent transition-colors"
            title="Voltar à lista de planos"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>

          <div className="flex items-center gap-2">
            <ClipboardList className="h-5 w-5 text-primary shrink-0" />
            <div>
              <h1 className="text-lg font-semibold text-foreground leading-tight">
                {plan.name}
              </h1>
              {/* Nome do cliente, se o plano não for um template */}
              {plan.client_id && clients.length > 0 && (
                <p className="text-xs text-muted-foreground">
                  {clients.find((c) => c.id === plan.client_id)?.full_name ??
                    ''}
                </p>
              )}
            </div>
          </div>
        </div>

        {/* Badges e ações */}
        <div className="flex items-center gap-3 flex-wrap">
          <Badge variant="outline" className={statusStyle.className}>
            {statusStyle.label}
          </Badge>

          {/*
            Botão "Ativar para cliente" — só aparece em planos publicados.
            Planos em rascunho ou arquivados não devem ser ativados.
          */}
          {plan.status === 'published' && (
            <Button
              size="sm"
              onClick={() => setActivateOpen(true)}
              className="bg-primary text-primary-foreground hover:bg-primary/90 gap-2"
            >
              <Zap className="h-3.5 w-3.5" />
              Ativar para Cliente
            </Button>
          )}
        </div>
      </div>

      {/* ── Descrição / Notas do plano ── */}
      {plan.notes && (
        <p className="text-sm text-muted-foreground whitespace-pre-wrap max-w-2xl">
          {plan.notes}
        </p>
      )}

      {/* ── Lista de dias ── */}
      <div className="max-w-3xl">
        {/* Skeleton só na carga inicial (days ainda vazios) */}
        {loadingDays && days.length === 0 ? (
          <div className="space-y-3">
            {[1, 2, 3].map((i) => (
              <div key={i} className="h-12 rounded-lg bg-muted animate-pulse" />
            ))}
          </div>
        ) : (
          <PlanDaysList
            planId={plan.id}
            days={days}
            onRefresh={handleRefreshDays}
          />
        )}
      </div>

      {/* Dialog: ativar plano para cliente */}
      <ActivatePlanDialog
        open={activateOpen}
        onOpenChange={setActivateOpen}
        plan={plan}
        clients={clients}
        onActivate={handleActivate}
      />
    </div>
  );
}
