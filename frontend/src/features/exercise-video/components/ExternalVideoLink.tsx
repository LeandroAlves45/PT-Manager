import { ExternalLink } from 'lucide-react';

import { externalVideoLink } from '@/features/exercise-video/lib/externalVideo';
import { Button } from '@/shared/components/ui/button';

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
      <a href={link.href} target="_blank" rel="noopener noreferrer">
        <ExternalLink aria-hidden />
        Ver no {link.provider} <span className="sr-only">(abre noutro separador)</span>
      </a>
    </Button>
  );
}
