import { useId, useRef, useState } from 'react';
import { toast } from 'sonner';

import { useRemoveAvatarMutation, useReplaceAvatarMutation } from '@/features/portal/api/portal';
import { ClientAvatar } from '@/features/portal/components/ClientAvatar';
import { PortalCard } from '@/features/portal/components/PortalCard';
import { avatarFailureMessage, avatarFileError } from '@/features/portal/lib/profile';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { Button } from '@/shared/components/ui/button';

type Profile = components['schemas']['MyProfileResponse'];

/**
 * Fotografia do perfil: enviar (PNG, JPEG ou WebP até 4 MB) e remover.
 *
 * O ficheiro é validado localmente pelo tipo e pelo tamanho; o servidor ainda descodifica a
 * imagem, confirma as dimensões e passa-a pela moderação antes de a guardar. A moderação é
 * síncrona: ou a fotografia fica publicada na resposta, ou o pedido é recusado e nada muda.
 * Não há estado "em análise".
 *
 * Sem pré-visualização local: a fotografia só aparece depois de aceite, para o cliente nunca
 * ver no ecrã uma imagem que o servidor recusou.
 *
 * @param profile Perfil atual (`avatar_url` é a fotografia publicada, ou `null`).
 */
export function AvatarSection({ profile }: { profile: Profile }) {
  const replace = useReplaceAvatarMutation();
  const remove = useRemoveAvatarMutation();
  const [error, setError] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const inputId = useId();
  const errorId = `${inputId}-error`;

  async function upload(file: File) {
    const invalid = avatarFileError(file);
    if (invalid !== null) {
      setError(invalid);
      if (inputRef.current !== null) inputRef.current.value = '';
      return;
    }
    setError(null);

    try {
      await replace.mutateAsync(file);
      toast.success('Fotografia atualizada com sucesso.');
    } catch (failure) {
      setError(avatarFailureMessage(failure, 'Não foi possível atualizar a fotografia.'));
    } finally {
      // Permite voltar a escolher o mesmo ficheiro depois da recusa.
      if (inputRef.current !== null) inputRef.current.value = '';
    }
  }

  async function removeAvatar() {
    try {
      await remove.mutateAsync(undefined);
      setConfirmRemove(false);
      toast.success('Fotografia removida com sucesso.');
    } catch (failure) {
      toast.error(avatarFailureMessage(failure, 'Não foi possível remover a fotografia.'));
    }
  }

  return (
    <PortalCard title="Fotografia">
      <p className="text-muted-foreground text-sm">PNG, JPEG ou WebP, até 4 MB.</p>

      <div className="flex flex-wrap items-center gap-4 pt-2">
        <ClientAvatar
          name={profile.name}
          avatarUrl={profile.avatar_url}
          className="size-20 text-2xl"
        />
        <div className="flex flex-wrap gap-2">
          <label htmlFor={inputId} className="sr-only">
            Ficheiro da fotografia
          </label>
          <input
            ref={inputRef}
            id={inputId}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="sr-only"
            aria-invalid={error !== null}
            aria-describedby={error === null ? undefined : errorId}
            disabled={replace.isPending}
            onChange={(event) => {
              const file = event.target.files?.[0];
              if (file !== undefined) void upload(file);
            }}
          />
          <Button
            type="button"
            className="min-h-11"
            disabled={replace.isPending}
            onClick={() => inputRef.current?.click()}
          >
            {replace.isPending
              ? 'A enviar…'
              : profile.avatar_url === null
                ? 'Enviar fotografia'
                : 'Substituir fotografia'}
          </Button>
          {profile.avatar_url !== null && (
            <Button
              type="button"
              variant="outline"
              className="min-h-11"
              disabled={replace.isPending}
              onClick={() => setConfirmRemove(true)}
            >
              Remover
            </Button>
          )}
        </div>
      </div>

      {error !== null && (
        <p id={errorId} role="alert" className="text-destructive text-sm">
          {error}
        </p>
      )}

      <ConfirmDialog
        open={confirmRemove}
        onOpenChange={setConfirmRemove}
        title="Remover a fotografia?"
        description="O teu perfil volta a mostrar as iniciais do teu nome."
        confirmLabel="Remover fotografia"
        pendingLabel="A remover…"
        destructive
        pending={remove.isPending}
        onConfirm={() => void removeAvatar()}
      />
    </PortalCard>
  );
}
