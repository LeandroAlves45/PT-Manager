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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Copy } from 'lucide-react';

/**
 * * Dialog para criar ou editar um plano de treino.
 *
 * Modo criar: plan=null → campos vazios
 * Modo editar: plan=objeto → campos pré-preenchidos
 *
 * @param {boolean} open - Controla visibilidade do dialog
 * @param {Function} onOpenChange - Chamada ao fechar (false)
 * @param {Object|null} plan - Plano para editar, ou null para criar
 * @param {Array} clients - Lista de clientes para o select
 * @param {Function} onSave - Callback com payload ao submeter
 */

export default function PlanFormDialog({
  open,
  onOpenChange,
  plan,
  clients = [],
  onSave,
}) {
  //useForm do react-hook-form para controle dos campos
  const isEdit = Boolean(plan); //true se plan tem valor, false se null (modo criar)

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      name: '',
      client_id: '',
      status: 'draft',
      notes: '',
    },
  });

  // Sincroniza o formulário sempre que o dialog abre ou o plano muda.
  // Sem este useEffect, ao abrir edição de um plano diferente os campos
  // mostrariam os valores do plano anterior (react-hook-form mantém estado).
  useEffect(() => {
    if (open) {
      reset({
        name: plan?.name || '',
        client_id: plan?.client_id || '',
        status: plan?.status || 'draft',
        notes: plan?.notes || '',
      });
    }
  }, [open, plan, reset]);

  // Função chamada ao submeter o formulário
  const onSubmit = async (data) => {
    await onSave({
      name: data.name.trim(),
      client_id: data.client_id || null,
      status: data.status,
      notes: data.notes.trim(),
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="bg-card border-border text-foreground sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {isEdit ? 'Editar Plano de Treino' : 'Criar Plano de Treino'}
          </DialogTitle>
          <DialogDescription className="text-muted-foreground">
            {isEdit
              ? 'Atualiza os detalhes do plano.'
              : 'Deixa "Cliente" vazio para criar um template reutilizável.'}
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="flex flex-col gap-4 mt-2"
        >
          {/* Campo Nome */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="plan_name">
              Nome do Plano <span className="text-destructive">*</span>
            </Label>
            <Input
              id="plan_name"
              placeholder="Ex: Hipertrofia, Adaptação"
              {...register('name', {
                required: 'Nome do plano é obrigatório',
                minLength: {
                  value: 1,
                  message: 'Nome do plano não pode estar vazio.',
                },
              })}
            />
          </div>

          {/* Campo Cliente */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="client_id">
              Cliente{' '}
              <span className="text-muted-foreground text-xs">
                (Vazio = template)
              </span>
            </Label>
            <Controller
              name="client_id"
              control={control}
              render={({ field }) => (
                <Select
                  value={field.value || 'template'}
                  onValueChange={(val) =>
                    field.onChange(val === 'template' ? '' : val)
                  }
                >
                  <SelectTrigger className="bg-background border-input">
                    <SelectValue placeholder="Selecione um cliente..." />
                  </SelectTrigger>
                  <SelectContent className="bg-popover border-border">
                    <SelectItem value="template">
                      <div className="flex items-center gap-2">
                        <Copy className="h-4 w-4 text-muted-foreground" />
                        <span>Template (sem cliente)</span>
                      </div>
                    </SelectItem>
                    {clients
                      .filter((c) => c.status === 'active')
                      .map((client) => (
                        <SelectItem key={client.id} value={client.id}>
                          {client.full_name}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              )}
            />
          </div>

          {/* Status */}
          <div className="flex flex-col gap-1.5">
            <Label>Status</Label>
            <Controller
              name="status"
              control={control}
              render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange}>
                  <SelectTrigger className="bg-background border-input">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent className="bg-popover border-border">
                    {/* draft=a construir | published=pronto | archived=inativo */}
                    <SelectItem value="draft">Rascunho</SelectItem>
                    <SelectItem value="published">Publicado</SelectItem>
                    <SelectItem value="archived">Arquivado</SelectItem>
                  </SelectContent>
                </Select>
              )}
            />
          </div>

          {/* Campo Notas */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="plan_notes">Notas</Label>
            <Textarea
              id="plan_notes"
              placeholder="Anotações, observações ou instruções para este plano..."
              className="bg-background border-input resize-none"
              rows={3}
              {...register('notes')}
            />
          </div>

          {/* Botões */}
          <div className="flex justify-end gap-3 pt-2">
            <Button
              type="button"
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
              {isSubmitting
                ? 'A guardar...'
                : isEdit
                  ? 'Guardar Alterações'
                  : 'Criar Plano'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
