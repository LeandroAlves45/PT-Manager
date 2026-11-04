import { ExternalLink } from 'lucide-react';

import { externalVideoLink } from '@/features/exercise-video/lib/externalVideo';
import { Button } from '@/shared/components/ui/button';
import { isNativeApp } from '@/shared/native/platform';
import { openExternalUrl } from '@/shared/native/shell';

/**
 * Botão que abre a ligação de vídeo externa noutro separador.
 *
 * `noopener` impede a página aberta de mexer nesta (`window.opener`) e `noreferrer` não envia o
 * endereço da app ao YouTube ou ao Vimeo. Um endereço fora da lista não desenha nada.
 *
 * @param url `video_url` do exercício.
 */
export function ExternalVideoLink({ url }: { url: string | null | undefined }) {
  const link = externalVideoLink(url);
  if (link === null) return null;

  return (
    <Button asChild variant="outline" size="sm" className="min-h-11 md:min-h-8">
      <a
        href={link.href}
        target="_blank"
        rel="noopener noreferrer"
        onClick={(event) => {
          // Na app, um target="_blank" não abre nada: abre-se no browser do sistema.
          if (!isNativeApp()) return;
          event.preventDefault();
          void openExternalUrl(link.href);
        }}
      >
        <ExternalLink aria-hidden />
        Ver no {link.provider} <span className="sr-only">(abre noutro separador)</span>
      </a>
    </Button>
  );
}
