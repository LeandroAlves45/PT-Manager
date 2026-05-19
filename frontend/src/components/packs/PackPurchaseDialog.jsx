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
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectTrigger,
  SelectValue,
  SelectItem,
} from '@/components/ui/select';
import { Package, Check } from 'lucide-react';
import { getPackTypes } from '@/api/packTypesApi';

/**
 * Dialog modal para atribuir um pack a um cliente.
 *
 * Fluxo:
 * 1. Carrega tipos de packs disponíveis da API
 * 2. Utilizador seleciona tipo de pack
 * 3. Mostra preview das sessões incluídas
 * 4. Ao confirmar, chama callback onPurchase
 *
 * @param {Object} props
 * @param {boolean} props.open - Controla se o dialog está aberto
 * @param {Function} props.onOpenChange - Callback quando o estado de abertura muda
 * @param {Object|null} props.client - Cliente selecionado (null se dialog fechado)
 * @param {Function} props.onPurchase - Callback com (clientId, packTypeId) quando utilizador confirma
 */

function PackPurchaseDialog({ open, onOpenChange, client, onPurchase }) {
  //Estado local
  const [packTypes, setPackTypes] = useState([]);
  const [loadingPackTypes, setLoadingPackTypes] = useState(false);
  const [selectedPackType, setSelectedPackType] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  //React Hook Form para gerir o formulário
  const { handleSubmit, setValue, watch, reset } = useForm({
    defaultValues: {
      pack_type_id: '',
    },
  });

  /**
   * Carrega tipos de packs disponíveis quando o dialog abre.
   * Usa useEffect para fazer fetch apenas quando necessário.
   */
  useEffect(() => {
    const fetchPackTypes = async () => {
      if (open) return; //Não carregar se dialog fechado

      try {
        setLoadingPackTypes(true);
        const types = await getPackTypes();
        setPackTypes(types);
      } catch (error) {
        console.error('Erro ao carregar tipos de packs:', error);
      } finally {
        setLoadingPackTypes(false);
      }
    };

    fetchPackTypes();
  }, [open]);

  /**
   * Reseta o formulário quando o dialog abre/fecha.
   * Limpa seleção anterior.
   */
  useEffect(() => {
    if (!open) {
      setSelectedPackType('');
      reset({ pack_type_id: '' });
    }
  }, [open, reset]);

  /**
   * Processa a compra do pack.
   *
   * @param {Object} data - Dados do formulário
   */
  const onSubmit = async (data) => {
    if (!selectedPackType || !client) return;

    try {
      setIsSubmitting(true);
      //Chama callback do pai para processar com clientId e packTypeId
      await onPurchase(client.id, selectedPackType);
      onOpenChange(false);
    } catch (error) {
      console.error('Erro ao processar compra do pack:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  /**
   * Encontra o pack type selecionado para mostrar detalhes.
   * Retorna null se nenhum selecionado ou ainda está carregando.
   */
  const selectedPack = packTypes.find((pt) => pt.id === selectedPackType);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="bg-card border-border text-foreground sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="text-foreground flex items-center gap-2">
            <Package className="h-5 w-5 text-primary" />
            Atribuir Pack
          </DialogTitle>
          <DialogDescription className="text-muted-foreground">
            {client ? (
              <>
                Atribuir um pack de sessões para{' '}
                <span className="font-medium text-foreground">
                  {client.full_name}
                </span>
              </>
            ) : (
              'Selecione um cliente primeiro'
            )}
          </DialogDescription>
        </DialogHeader>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="flex flex-col gap-4 mt-2"
        >
          {/* CAMPO: Tipo de Pack (obrigatório) */}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="pack_type">Tipo de Pack *</Label>
            <Select
              value={selectedPackType}
              onValueChange={(val) => {
                setSelectedPackType(val);
                setValue('pack_type_id', val); // Sincroniza com react-hook-form
              }}
            >
              <SelectTrigger className="bg-background border-input text-foreground">
                <SelectValue placeholder="Selecione um pack" />
              </SelectTrigger>
              <SelectContent className="bg-popover border-border">
                {loadingPackTypes ? (
                  // Loading state
                  <SelectItem value="loading" disabled>
                    A carregar packs...
                  </SelectItem>
                ) : packTypes.length === 0 ? (
                  // Estado vazio - sem packs configurados
                  <SelectItem value="empty" disabled>
                    Nenhum pack disponível
                  </SelectItem>
                ) : (
                  // Lista de tipos de pack
                  packTypes.map((packType) => (
                    <SelectItem key={packType.id} value={packType.id}>
                      <div className="flex items-center justify-between w-full">
                        <span className="font-medium">{packType.name}</span>
                        <span className="text-xs text-muted-foreground ml-3">
                          {packType.sessions_total} sessões
                        </span>
                      </div>
                    </SelectItem>
                  ))
                )}
              </SelectContent>
            </Select>
          </div>

          {/* PREVIEW DO PACK SELECIONADO */}
          {selectedPack && (
            <div className="rounded-lg border border-border bg-accent/50 p-4">
              <h4 className="text-sm font-medium text-foreground mb-3">
                Detalhes do Pack
              </h4>

              {/* Nome do pack */}
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-muted-foreground">Pack:</span>
                <span className="text-sm font-medium text-foreground">
                  {selectedPack.name}
                </span>
              </div>

              {/* Total de sessões */}
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-muted-foreground">Sessões:</span>
                <span className="text-sm font-medium text-primary">
                  {selectedPack.sessions_total} sessões
                </span>
              </div>

              {/* Descrição (se houver) */}
              {selectedPack.description && (
                <div className="mt-3 pt-3 border-t border-border">
                  <p className="text-xs text-muted-foreground">
                    {selectedPack.description}
                  </p>
                </div>
              )}

              {/* Informação sobre pack ativo */}
              {client?.active_pack && (
                <div className="mt-3 pt-3 border-t border-border">
                  <div className="flex items-start gap-2 text-xs text-amber-600 dark:text-amber-500">
                    <Package className="h-4 w-4 mt-0.5 shrink-0" />
                    <div>
                      <p className="font-medium">Pack ativo existente</p>
                      <p className="text-muted-foreground mt-1">
                        O cliente já tem um pack ativo (
                        {client.active_pack.pack_type_name}) com{' '}
                        {client.active_pack.sessions_remaining} sessões
                        restantes. Ao atribuir um novo pack, ele será adicionado
                        à conta.
                      </p>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Mensagem se nenhum pack disponível */}
          {!loadingPackTypes && packTypes.length === 0 && (
            <div className="rounded-lg border border-border bg-destructive/10 p-4">
              <p className="text-sm text-destructive">
                Nenhum tipo de pack configurado. Crie tipos de pack primeiro na
                página de Packs.
              </p>
            </div>
          )}

          {/* BOTÕES DE AÇÃO */}
          <div className="flex justify-end gap-3 pt-2">
            {/* Botão Cancelar */}
            <Button
              type="button"
              variant="ghost"
              onClick={() => onOpenChange(false)}
              className="text-muted-foreground hover:text-foreground"
              disabled={isSubmitting}
            >
              Cancelar
            </Button>

            {/* Botão Confirmar Compra */}
            <Button
              type="submit"
              disabled={isSubmitting || !selectedPackType || !client}
              className="bg-primary text-primary-foreground hover:bg-primary/90 gap-2"
            >
              {isSubmitting ? (
                'A processar...'
              ) : (
                <>
                  <Check className="h-4 w-4" />
                  Atribuir Pack
                </>
              )}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export default PackPurchaseDialog;
