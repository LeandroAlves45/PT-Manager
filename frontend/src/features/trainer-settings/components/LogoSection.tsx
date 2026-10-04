import { ImageOff } from 'lucide-react';
import { useId, useRef, useState } from 'react';
import { toast } from 'sonner';

import {
  useRemoveLogoMutation,
  useReplaceLogoMutation,
} from '@/features/trainer-settings/api/settings';
import { logoFileError, settingsErrorMessage } from '@/features/trainer-settings/lib/settings';
import type { components } from '@/shared/api/schema';
import { ConfirmDialog } from '@/shared/components/ConfirmDialog';
import { Button } from '@/shared/components/ui/button';

type Settings = components['schemas']['TrainerSettingsResponse'];

/**
 * Logo: enviar (PNG, JPEG ou WEBP até 5 MB) e remover.
 *
 * O ficheiro é validado localmente pelo tipo e pelo tamanho antes do pedido; o servidor
 * ainda descodifica a imagem, confirma as dimensões (64–4096 px) e reduz para 512 px WEBP.
 * O envio é um só pedido, sem progresso: 5 MB não justificam o XHR do vídeo.
 *
 * @param settings Definições atuais (`logo_url` é o logo publicado, ou `null`).
 */
export function LogoSection({ settings }: { settings: Settings }) {
  const replace = useReplaceLogoMutation();
  const remove = useRemoveLogoMutation();
  const [error, setError] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const inputId = useId();
  const errorId = `${inputId}-error`;

  async function upload(file: File) {
    const invalid = logoFileError(file);
    if (invalid !== null) {
      setError(invalid);
      if (inputRef.current !== null) inputRef.current.value = '';
      return;
    }
    setError(null);

    try {
      await replace.mutateAsync(file);
      toast.success('Logo atualizado com sucesso.');
    } catch (failure) {
      setError(settingsErrorMessage(failure, 'Não foi possível atualizar o logo.'));
    } finally {
      // Permite voltar a escolher o mesmo ficheiro depois de uma recusa.
      if (inputRef.current !== null) inputRef.current.value = '';
    }
  }

  async function removeLogo() {
    try {
      await remove.mutateAsync();
      setConfirmRemove(false);
      toast.success('Logo removido com sucesso.');
    } catch (failure) {
      toast.error(settingsErrorMessage(failure, 'Não foi possível remover o logo.'));
    }
  }

  return (
    <section aria-labelledby="logo-title" className="border-border space-y-4 rounded-xl border p-5">
      <div>
        <h2 id="logo-title" className="font-display text-xl">
          Logo
        </h2>
        <p className="text-muted-foreground text-sm">PNG, JPEG ou WebP, até 5 MB.</p>
      </div>
      <div className="flex flex-wrap items-center gap-4">
        <div className="border-border bg-muted flex size-20 items-center justify-center overflow-hidden rounded-xl border">
          {settings.logo_url === null ? (
            <ImageOff aria-hidden className="text-muted-foreground" />
          ) : (
            <img
              src={settings.logo_url}
              alt={`Logo de ${settings.app_name}`}
              className="size-full object-contain"
            />
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          <label htmlFor={inputId} className="sr-only">
            Ficheiro do logo
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
            disabled={replace.isPending}
            onClick={() => inputRef.current?.click()}
          >
            {replace.isPending
              ? 'A enviar…'
              : settings.logo_url === null
                ? 'Enviar logo'
                : 'Substituir logo'}
          </Button>
          {settings.logo_url !== null && (
            <Button type="button" variant="outline" onClick={() => setConfirmRemove(true)}>
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
        title="Remover o logo?"
        description="O portal dos teus clientes passa a mostrar só o nome da app."
        confirmLabel="Remover logo"
        pendingLabel="A remover…"
        destructive
        pending={remove.isPending}
        onConfirm={() => void removeLogo()}
      />
    </section>
  );
}
