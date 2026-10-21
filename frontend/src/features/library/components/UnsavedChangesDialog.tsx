import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { useUnsavedChangesGuard } from '@/shared/hooks/useUnsavedChangesGuard';

/**
 * Aviso «Sair sem guardar?» dos formulários da biblioteca.
 *
 * Bloqueia a navegação enquanto `when` for verdadeiro. Durante o envio de um vídeo o texto
 * diz que sair o cancela (o painel do vídeo desmonta).
 *
 * @param when `true` com alterações por guardar; o formulário passa `false` logo que guarda,
 *   para a navegação que o próprio guardar provoca não ser bloqueada.
 * @param uploading `true` enquanto um vídeo está a ser enviado.
 */
export function UnsavedChangesDialog({
  when,
  uploading = false,
}: {
  when: boolean;
  uploading?: boolean;
}) {
  const blocker = useUnsavedChangesGuard(when);

  return (
    <ConfirmDialog
      open={blocker.state === 'blocked'}
      onOpenChange={(open) => {
        if (!open) blocker.reset?.();
      }}
      title={uploading ? 'Sair durante o envio do vídeo?' : 'Sair sem guardar?'}
      description={
        uploading
          ? 'O vídeo ainda está a ser enviado. Se saíres, o envio é cancelado.'
          : 'As alterações a este item perdem-se.'
      }
      confirmLabel="Sair sem guardar"
      cancelLabel="Continuar a editar"
      destructive
      pending={false}
      onConfirm={() => blocker.proceed?.()}
    />
  );
}
