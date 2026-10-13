import { useState } from 'react';

import { profileInitials } from '@/features/portal/lib/profile';
import { cn } from '@/shared/lib/utils';

/**
 * Fotografia do cliente, ou as iniciais do nome quando não há fotografia.
 *
 * É decorativa (`alt=""` e iniciais com `aria-hidden`): o nome está sempre escrito ao lado
 * ou no rótulo do botão que a contém. Se a imagem não carregar (o ficheiro pode ter sido
 * apagado no armazenamento), voltam as iniciais em vez de um ícone partido.
 *
 * @param className Tamanho do círculo (`size-7` no topo, `size-20` no perfil).
 */
export function ClientAvatar({
  name,
  avatarUrl,
  className,
}: {
  name: string;
  avatarUrl: string | null;
  className?: string;
}) {
  // Guarda o URL que falhou, não booleano: uma fotografia nova tem outro URL e volta a tentar.
  const [failedUrl, setFailedUrl] = useState<string | null>(null);

  if (avatarUrl !== null && avatarUrl !== failedUrl)
    return (
      <img
        src={avatarUrl}
        alt=""
        data-testid="client-avatar-image"
        className={cn('shrink-0 rounded-full object-cover', className)}
        onError={() => setFailedUrl(avatarUrl)}
      />
    );

  return (
    <span
      aria-hidden
      data-testid="client-avatar-initials"
      className={cn(
        'bg-muted text-muted-foreground flex shrink-0 items-center justify-center rounded-full font-medium',
        className
      )}
    >
      {profileInitials(name)}
    </span>
  );
}
