import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
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
  SelectTrigger,
  SelectValue,
  SelectItem,
} from '@/components/ui/select';
import { useClients } from '@/hooks/useClients';

/**
 * Dialog modal para agendar uma nova sessão
 *
 * Componente controlado:
 * - Lista todos os clientes ativos para seleção
 * - Permite escolher data, hora, duração e local
 * - Valida campos obrigatórios
 * - Formata dados antes de enviar para API
 *
 * @param {Object} props
 * @param {boolean} props.open - Controla abertura do dialog
 * @param {function} props.onOpenChange - Callback para mudança de estado do dialog
 * @param {function} props.onSave - Callback com os dados formatados para salvar a sessão
 */
function SessionFormDialog({ open, onOpenChange, onSave }) {
  // Hook para obter lista de clientes ativos
  const { clients, loading: loadingClients } = useClients({ Status: 1 });

  // Estado local para controle de loading do formulário
  const [selectedClient, setSelectedClient] = useState('');

  // React Hook Form para controle do formulário
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      client_id: '',
      session_date: '',
      session_time: '',
      duration_minutes: 60,
      location: '',
      notes: '',
    },
  });

  /**
   *
   * Quando o dialog é aberto, reseta o formulário e limpa o cliente selecionado
   * Limpa seleção de cliente para evitar confusão se o usuário abrir o dialog várias vezes
   */
  useEffect(() => {
    if (open) {
      reset({
        client_id: '',
        session_date: '',
        session_time: '',
        duration_minutes: 60,
        location: '',
        notes: '',
      });
    }
  }, [open, reset]);

  /**
   * Processa os dados do formulário antes de enviar para o callback onSave
   *
   * Transformações realizadas:
   * - Combina date + time em ISO datetime
   * - Converte duration_minutes para número
   * - Remove campos vazios (transforma em null)
   * - Adiciona client_id da seleção
   *
   * @param {Object} data - Dados brutos do formulário
   */

  const onSubmit = (data) => {
    // Combina date + time em ISO datetime
    const starts_at = `${data.session_date}T${data.session_time}:00`;

    //Monta payload formatado para API
    const payload = {
      client_id: selectedClient,
      starts_at,
      duration_minutes: Number(data.duration_minutes),
      location: data.location || null,
      notes: data.notes || null,
    };

    // Chama callback onSave com payload formatado
    onSave(payload);
  };

  /**
   * Opções de duração pré-definidas para o select
   * Duração em minutos e label correspondente
   */
  const durationOptions = [
    { value: 30, label: '30 minutos' },
    { value: 45, label: '45 minutos' },
    { value: 60, label: '1 hora' },
    { value: 90, label: '1 hora e 30 minutos' },
    { value: 120, label: '2 horas' },
  ];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="bg-card border-border text-foreground sm:max-w-md max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-foreground">
            Agendar Nova Sessão
          </DialogTitle>
          <DialogDescription className="text-muted-foreground">
            Preencha os detalhes da sessão para agendar um novo atendimento.
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="flex flex-col gap-4 mt-2"
        >
          {/* Select de Cliente */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="client">Cliente</Label>
            <Select
              value={selectedClient}
              onValueChange={(value) => {
                setSelectedClient(value);
                setValue('client_id', value); //sincroniza valor do select com o formulário
              }}
            >
              <SelectTrigger className="bg-background border-input text-foreground">
                <SelectValue placeholder="Selecione um cliente" />
              </SelectTrigger>
              <SelectContent className="bg-popover border-border">
                {loadingClients ? (
                  //Loading state enquanto busca clientes
                  <SelectItem value="loading" disabled>
                    A carregar clientes...
                  </SelectItem>
                ) : clients.length === 0 ? (
                  //Estado vazio caso não haja clientes ativos
                  <SelectItem value="empty" disabled>
                    Nenhum cliente ativo encontrado
                  </SelectItem>
                ) : (
                  //Lista de clientes ativos para seleção
                  clients.map((client) => (
                    <SelectItem key={client.id} value={client.id}>
                      {client.full_name}
                      {/* Mostra pack ativo se existir */}
                      {client.active_pack && (
                        <span className="text-sm text-muted-foreground ml-2">
                          ({client.active_pack.sessions_remaining} sessões
                          restantes)
                        </span>
                      )}
                    </SelectItem>
                  ))
                )}
              </SelectContent>
            </Select>
            {/* Mensagem de erro se campo não preenchido */}
            {!selectedClient && errors.client_id && (
              <span className="text-xs text-destructive">
                Selecione um cliente para agendar a sessão
              </span>
            )}
          </div>

          {/* LINHA: Data e Hora */}
          <div className="grid grid-cols-2 gap-4">
            {/* Campo de Data (obrigatório) */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="session_date">Data</Label>
              <Input
                id="session_date"
                type="date"
                {...register('session_date', {
                  required: 'A data da sessão é obrigatória',
                })}
                className="bg-background border-input text-foreground"
              />
              {errors.session_date && (
                <span className="text-xs text-destructive">
                  {errors.session_date.message}
                </span>
              )}
            </div>

            {/* Campo de Hora (obrigatório) */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="session_time">Hora</Label>
              <Input
                id="session_time"
                type="time"
                {...register('session_time', {
                  required: 'A hora da sessão é obrigatória',
                })}
                className="bg-background border-input text-foreground"
              />
              {errors.session_time && (
                <span className="text-xs text-destructive">
                  {errors.session_time.message}
                </span>
              )}
            </div>
          </div>

          {/* Campo de Duração */}
          <div className="flex flex-col gap-1.5">
            <Label>Duração *</Label>
            <Select
              value={String(watch('duration_minutes') || 60)} // Garante que o valor seja string para o select
              onValueChange={(value) =>
                setValue('duration_minutes', Number(value))
              } // Converte de volta para número ao atualizar o formulário
            >
              <SelectTrigger className="bg-background border-input text-foreground">
                <SelectValue placeholder="Selecione a duração" />
              </SelectTrigger>
              <SelectContent className="bg-popover border-border">
                {durationOptions.map((option) => (
                  <SelectItem key={option.value} value={String(option.value)}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Campo de Local */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="location">Local</Label>
            <Input
              id="location"
              {...register('location')}
              className="bg-background border-input text-foreground"
              placeholder="Ex: Ginásio, Online, Casa do cliente..."
            />
          </div>

          {/* Campo de Notas */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="notes">Notas</Label>
            <Textarea
              id="notes"
              {...register('notes')}
              className="bg-background border-input text-foreground resize-none"
              placeholder="Notas adicionais sobre a sessão"
              rows={3}
            />
          </div>

          {/* Botões de Ação */}
          <div className="flex justify-end gap-3 pt-2">
            {/* Botão de Cancelar fecha o dialog sem salvar */}
            <Button
              type="button"
              variant="ghost"
              onClick={() => onOpenChange(false)}
              className="text-muted-foreground hover:text-foreground"
            >
              Cancelar
            </Button>

            {/* Botão de Salvar chama o onSubmit para processar os dados */}
            <Button
              type="submit"
              disabled={isSubmitting || !selectedClient} // Desabilita o botão enquanto o formulário está sendo submetido
              className="bg-primary text-primary-foreground hover:bg-primary/90"
            >
              {isSubmitting ? 'A agendar...' : 'Agendar Sessão'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export default SessionFormDialog;
