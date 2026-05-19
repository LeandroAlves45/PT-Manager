import { useEffect } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { videoUrlRules } from '@/utils/validators';
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
import { Switch } from '@/components/ui/switch';
import MuscleMultiSelect from './MuscleMultiSelect';

/**
 * Dialog para criar ou editar um exercício.
 *
 * Modo criar: exercise=null → campos vazios
 * Modo editar: exercise=objeto → campos pré-preenchidos
 *
 * O campo muscles usa o MuscleMultiSelect que converte entre:
 *   - Array no UI: ["Bicep", "Tricep"]
 *   - String no backend: "Bicep, Tricep"
 *
 * @param {boolean}     open          - Controla visibilidade do dialog
 * @param {Function}    onOpenChange  - Chamada ao fechar
 * @param {Object|null} exercise      - Exercício para editar, null para criar
 * @param {Function}    onSave        - Callback com payload ao submeter
 */

export default function ExerciseFormDialog({
  open,
  onOpenChange,
  exercise,
  onSave,
}) {
  const isEdit = Boolean(exercise); //true se exercise tem valor, false se null (modo criar)

  const {
    register,
    handleSubmit,
    control,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      name: '',
      muscles: '',
      url: '',
      is_active: true,
    },
  });

  // Sincroniza o form quando o dialog abre ou muda o exercício
  // Sem este useEffect, ao editar exercícios diferentes os campos
  // mostrariam os dados do exercício anterior
  useEffect(() => {
    if (open) {
      reset({
        name: exercise?.name || '',
        muscles: exercise?.muscles || '',
        url: exercise?.url || '',
        is_active: exercise?.is_active ?? true,
      });
    }
  }, [open, exercise, reset]);

  const onSubmit = async (data) => {
    await onSave({
      name: data.name.trim(),
      muscles: data.muscles.trim(), // MuscleMultiSelect já devolve string "Bicep, Tricep"
      url: data.url.trim(),
      is_active: data.is_active,
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="bg-card border-border text-foreground sm:max-w-md">
        <DialogHeader>
          <DialogTitle>
            {isEdit ? 'Editar Exercício' : 'Novo Exercício'}
          </DialogTitle>
          <DialogDescription className="text-muted-foreground">
            {isEdit
              ? 'Atualiza os detalhes do exercício.'
              : 'Preencha os detalhes para criar um novo exercício.'}
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="flex flex-col gap-4 mt-2"
        >
          {/* Nome */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="exercise_name">
              Nome <span className="text-destructive">*</span>
            </Label>
            <Input
              id="exercise_name"
              placeholder="Ex: Supino Plano com Barra"
              className="bg-background border-input"
              {...register('name', {
                required: 'Nome é obrigatório',
                minLength: { value: 1, message: 'Nome não pode estar vazio' },
              })}
            />
            {errors.name && (
              <span className="text-destructive text-xs">
                {errors.name.message}
              </span>
            )}
          </div>

          {/* Músculos — usa Controller porque MuscleMultiSelect não expõe ref nativo */}
          <div className="flex flex-col gap-1.5">
            <Label>
              Músculos <span className="text-destructive">*</span>
            </Label>
            <Controller
              name="muscles"
              control={control}
              rules={{ required: 'Seleciona pelo menos um músculo' }}
              render={({ field }) => (
                <MuscleMultiSelect
                  // field.value é a string "Bicep, Tricep"
                  // field.onChange recebe nova string do MuscleMultiSelect
                  value={field.value}
                  onChange={field.onChange}
                  error={errors.muscles?.message}
                />
              )}
            />
          </div>

          {/* URL do vídeo */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="exercise_url">
              URL do Vídeo{' '}
              <span className="text-muted-foreground text-xs">(opcional)</span>
            </Label>
            <Input
              id="exercise_url"
              type="url"
              placeholder="https://youtube.com/..."
              className="bg-background border-input"
              {...register('url', videoUrlRules)}
            />
            {errors.url && (
              <span className="text-destructive text-xs">
                {errors.url.message}
              </span>
            )}
          </div>

          {/* Toggle Ativo/Inativo */}
          <div className="flex items-center justify-between py-1">
            <div className="flex flex-col gap-0.5">
              <Label>Ativo</Label>
              <span className="text-xs text-muted-foreground">
                Exercícios inativos não aparecem no picker
              </span>
            </div>
            {/*
              Controller necessário porque Switch do shadcn/ui usa checked/onCheckedChange
              em vez do onChange padrão do react-hook-form
            */}
            <Controller
              name="is_active"
              control={control}
              render={({ field }) => (
                <Switch
                  checked={field.value}
                  onCheckedChange={field.onChange}
                />
              )}
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
                  : 'Criar Exercício'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
