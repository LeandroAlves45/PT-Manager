import { useEffect } from 'react';
import { useForm, Controller } from 'react-hook-form';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Zap } from 'lucide-react';

/**
 * Dialog para ativar um plano de treino para um cliente.
 *
 * Permite:
 * - Escolher o cliente (se o plano for template)
 * - Definir a data de início
 * - Opção de ativar imediatamente ou na data escolhida
 *
 * @param {boolean} open
 * @param {Function} onOpenChange
 * @param {Object} plan - Plano a ser ativado
 * @param {Array} clients - Lista de clientes ativos
 * @param {Function} onActivate - Callback com { client_id, active_from }
 */

export default function ActivatePlanDialog({
  open,
  onOpenChange,
  plan,
  clients = [],
  onActivate,
}) {
  const isTemplate = plan?.client_id === null; // Se o plano não tem client_id, é um template
  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      //Se ja tem clientem pré seleciona, se for template deixa vazio para escolher
      client_id: plan?.client_id || '',
      //Data de início padrão: hoje
      active_from: new Date().toISOString().split('T')[0], //data atual no formato YYYY-MM-DD
    },
  });

  useEffect(() => {
    if (open) {
      reset({
        client_id: plan?.client_id || '',
        active_from: new Date().toISOString().split('T')[0],
      });
    }
  }, [open, plan, reset]);

  const onSubmit = async (data) => {
    await onActivate({
      client_id: data.client_id,
      active_from: data.active_from,
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="bg-card border-border text-foreground sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Zap className="h-5 w-5 text-primary" />
            Ativar Plano de Treino
          </DialogTitle>
          <DialogDescription className="text-muted-foreground">
            Ativa{' '}
            <span className="font-medium text-foreground">{plan?.name}</span>{' '}
            para um cliente. Se o cliente já tem plano ativo, será encerrado
            automaticamente.
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="flex flex-col gap-4 mt-2"
        >
          {/* CLIENTE - só mostra se for template (planos de cliente já têm client_id) */}
          {isTemplate && (
            <div className="flex flex-col gap-1.5">
              <Label>Cliente *</Label>
              <Controller
                name="client_id"
                control={control}
                rules={{ required: 'Selecione um cliente' }}
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger className="bg-background border-input">
                      <SelectValue placeholder="Selecione um cliente..." />
                    </SelectTrigger>
                    <SelectContent className="bg-popover border-border">
                      {ClientDetails.filter((c) => c.status === 'active').map(
                        (c) => (
                          <SelectItem key={c.id} value={c.id}>
                            {c.full_name}
                          </SelectItem>
                        )
                      )}
                    </SelectContent>
                  </Select>
                )}
              />
              {errors.client_id && (
                <p className="text-xs text-destructive">
                  {errors.client_id.message}
                </p>
              )}
            </div>
          )}

          {/* Data de início */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="active_from">Data de Início *</Label>
            <Input
              id="active_from"
              type="date"
              className="bg-background border-input"
              {...register('active_from', {
                required: 'Selecione a data de início',
              })}
            />
            {errors.active_from && (
              <p className="text-xs text-destructive">
                {errors.active_from.message}
              </p>
            )}
          </div>

          {/* Botões de ação */}
          <div className="flex-justify-end gap-3 pt-2">
            <Button
              type
              variant="ghost"
              onClick={() => onOpenChange(false)}
              disabled={isSubmitting}
              className="text-muted-foreground"
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              disabled={isSubmitting}
              className="bg-primary text-primary-foreground hover:bg-primary/90"
            >
              {isSubmitting ? 'A ativar...' : 'Ativar Plano'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
