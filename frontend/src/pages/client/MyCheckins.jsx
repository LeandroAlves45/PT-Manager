/**
 * MyCheckIns.jsx — lista de check-ins do cliente com formulário de resposta.
 *
 * O cliente vê os check-ins pendentes no topo com acção de responder,
 * seguidos do histórico de check-ins completados e ignorados.
 */

import { useEffect, useState, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { formatDate, parseNullableFloat, parseNullableInt } from '@/utils/formatters';
import { toast } from 'react-toastify';
import { ClipboardList, CheckCircle, Loader2, CalendarClock } from 'lucide-react';
import { getMyCheckIns, respondToCheckIn } from '@/api/clientPortalApi';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';

// Converte o status para label e variante de badge
function getStatusBadge(status) {
  if (status === 'pending') return { label: 'Pendente', variant: 'default' };
  if (status === 'completed')
    return { label: 'Respondido', variant: 'secondary' };
  if (status === 'skipped') return { label: 'Ignorado', variant: 'outline' };
  return { label: status, variant: 'outline' };
}


export default function MyCheckIns() {
  const [checkIns, setCheckIns] = useState([]);
  const [loading, setLoading] = useState(true);
  // checkin seleccionado para responder
  const [responding, setResponding] = useState(null);
  const [submitLoading, setSubmitLoading] = useState(false);

  const fetchCheckIns = useCallback(async () => {
    try {
      const data = await getMyCheckIns();
      setCheckIns(data);
    } catch (error) {
      toast.error(error.response?.data?.detail || 'Erro ao carregar check-ins');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchCheckIns();
  }, [fetchCheckIns]);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm();

  function openRespondDialog(checkIn) {
    setResponding(checkIn);
    reset(); // limpa o formulário para nova resposta
  }

  async function onSubmitResponse(formData) {
    setSubmitLoading(true);
    try {
      await respondToCheckIn(responding.id, {
        weight_kg: parseNullableFloat(formData.weight_kg),
        body_fat: parseNullableFloat(formData.body_fat),
        client_notes: formData.client_notes || null,
        questionnaire: {
          energy_level: parseNullableInt(formData.energy_level),
          training_perfomance: parseNullableInt(formData.training_perfomance),
          stress_level: parseNullableInt(formData.stress_level),
          plan_adherence_pct: parseNullableInt(formData.plan_adherence_pct),
          injuries: formData.injuries || null,
        },
      });
      toast.success('Avaliação enviada com sucesso');
      setResponding(null);
      await fetchCheckIns(); // recarrega os check-ins para mostrar a resposta
    } catch (error) {
      toast.error(error?.response?.data?.detail ?? 'Erro ao enviar resposta');
    } finally {
      setSubmitLoading(false);
    }
  }

  const pending = checkIns.filter((ci) => ci.status === 'pending');
  const history = checkIns.filter((ci) => ci.status !== 'pending');

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-64">
        <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
      </div>
    );
  }

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Check-ins</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Responde ás avaliações do teu Personal Trainer
        </p>
      </div>

      {/* Check-ins pendentes */}
      {pending.length > 0 && (
        <section className="flex flex-col gap-3">
          <h2 className="text-sm font-semibold text-foreground uppercase tracking-wide">
            Pendentes ({pending.length})
          </h2>
          {pending.map((checkin) => (
            <Card key={checkin.id} className="border-primary/30 bg-primary/5">
              <CardContent className="p-4 flex items-center justify-between gap-4">
                <div className="flex items-center gap-3">
                  <ClipboardList className="h-5 w-5 text-primary shrink-0" />
                  <div>
                    <p className="text-sm font-medium text-foreground">
                      Avaliação solicitada
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Pedido em {formatDate(checkin.requested_at)}
                    </p>
                    {checkin.target_date && (
                      <p className="text-xs text-primary font-medium flex items-center gap-1 mt-0.5">
                        <CalendarClock className="h-3 w-3" />
                        Responde até {formatDate(checkin.target_date)}
                      </p>
                    )}
                  </div>
                </div>
                <Button size="sm" onClick={() => openRespondDialog(checkin)}>
                  Responder
                </Button>
              </CardContent>
            </Card>
          ))}
        </section>
      )}

      {/* Histórico */}
      {history.length > 0 && (
        <section className="flex flex-col gap-3">
          <h2 className="text-sm font-semibold text-foreground uppercase tracking-wide">
            Histórico
          </h2>
          {history.map((checkin) => {
            const { label, variant } = getStatusBadge(checkin.status);
            return (
              <Card key={checkin.id} className="border-border bg-card">
                <CardContent className="p-4 flex flex-col gap-2">
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-medium text-foreground">
                      {formatDate(checkin.requested_at)}
                    </p>
                    {checkin.target_date && (
                        <p className="text-xs text-muted-foreground flex items-center gap-1">
                          <CalendarClock className="h-3 w-3" />
                          Alvo: {formatDate(checkin.target_date)}
                        </p>
                      )}
                    <Badge variant={variant}>{label}</Badge>
                  </div>
                  {checkin.status === 'completed' && (
                    <div className="flex flex-wrap gap-4 text-xs text-muted-foreground">
                      {checkin.weight_kg && (
                        <span>
                          Peso:{' '}
                          <span className="text-foreground">
                            {checkin.weight_kg} kg
                          </span>
                        </span>
                      )}
                      {checkin.body_fat && (
                        <span>
                          BF:{' '}
                          <span className="text-foreground">
                            {checkin.body_fat}%
                          </span>
                        </span>
                      )}
                      {checkin.completed_at && (
                        <span>
                          Respondido:{' '}
                          <span className="text-foreground">
                            {formatDate(checkin.completed_at)}
                          </span>
                        </span>
                      )}
                    </div>
                  )}
                  {checkin.trainer_notes && (
                    <div className="rounded bg-muted p-2 mt-1">
                      <p className="text-xs text-muted-foreground">
                        Nota do Personal Trainer:
                      </p>
                      <p className="text-sm text-foreground mt-0.5">
                        {checkin.trainer_notes}
                      </p>
                    </div>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </section>
      )}

      {/* Estado vazio */}
      {checkIns.length === 0 && (
        <div className="rounded-lg border border-border bg-card p-8 flex flex-col items-center gap-3 text-center">
          <CheckCircle className="h-10 w-10 text-muted-foreground" />
          <p className="font-medium text-foreground">Tudo em dia!</p>
          <p className="text-sm text-muted-foreground">
            Não tens avaliações pendentes.
          </p>
        </div>
      )}

      {/* Dialog de resposta à avaliação */}
      <Dialog
        open={!!responding}
        onOpenChange={(open) => !open && setResponding(null)}
      >
        <DialogContent className="max-w-md max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Responder à Avaliação</DialogTitle>
          </DialogHeader>

          <form
            onSubmit={handleSubmit(onSubmitResponse)}
            className="flex flex-col gap-4 mt-2"
          >
            {/* Biometria */}
            <div className="grid grid-cols-2 gap-3">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="weight_kg">Peso (kg)</Label>
                <Input
                  id="weight_kg"
                  type="number"
                  step="0.1"
                  placeholder="70.5"
                  {...register('weight_kg', {
                    min: { value: 20, message: 'Mínimo 20 kg' },
                    max: { value: 400, message: 'Máximo 400 kg' },
                  })}
                />
                {errors.weight_kg && (
                  <p className="text-xs text-destructive">
                    {errors.weight_kg.message}
                  </p>
                )}
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="body_fat">Gordura (%)</Label>
                <Input
                  id="body_fat"
                  type="number"
                  step="0.1"
                  placeholder="15.0"
                  {...register('body_fat', {
                    min: { value: 0, message: 'Mín. 0%' },
                    max: { value: 100, message: 'Máx. 100%' },
                  })}
                />
              </div>
            </div>

            {/* Escalas 1-5 */}
            <div className="grid grid-cols-2 gap-3">
              {[
                { id: 'energy_level', label: 'Energia (1-5)' },
                { id: 'training_performance', label: 'Treino (1-5)' },
                { id: 'stress_level', label: 'Stress (1-5)' },
                { id: 'plan_adherence_pct', label: 'Aderência (%)' },
              ].map(({ id, label }) => (
                <div key={id} className="flex flex-col gap-1.5">
                  <Label htmlFor={id}>{label}</Label>
                  <Input
                    id={id}
                    type="number"
                    placeholder={id === 'plan_adherence_pct' ? '85' : '3'}
                    {...register(id)}
                  />
                </div>
              ))}
            </div>

            {/* Lesões */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="injuries">Lesões ou dores (opcional)</Label>
              <Input
                id="injuries"
                placeholder="Nenhuma"
                {...register('injuries')}
              />
            </div>

            {/* Notas */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="client_notes">
                Notas para o Personal Trainer:
              </Label>
              <Textarea
                id="client_notes"
                placeholder="Como te sentiste esta semana?"
                rows={3}
                {...register('client_notes')}
              />
            </div>

            <DialogFooter className="gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => setResponding(null)}
                disabled={submitLoading}
              >
                Cancelar
              </Button>
              <Button type="submit" disabled={submitLoading}>
                {submitLoading ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin mr-2" />A enviar...
                  </>
                ) : (
                  'Enviar resposta'
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
