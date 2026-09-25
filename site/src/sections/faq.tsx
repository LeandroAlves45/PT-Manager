import { Plus } from 'lucide-react';

import type { FaqItem } from '@/domain/types';
import { Container } from '@/ui/container';
import { SectionHeading } from '@/ui/section-heading';

interface FaqProps {
  items: readonly FaqItem[];
}

/**
 * Perguntas frequentes com `<details>`/`<summary>` nativos.
 *
 * Teclado, leitores de ecrã e "procurar na página" funcionam sem JavaScript e sem ARIA
 * manual. O atributo `name` torna-o um acordeão exclusivo (uma resposta aberta de cada vez).
 */
export function Faq({ items }: FaqProps) {
  return (
    <section id="faq" aria-labelledby="t-faq" className="py-[clamp(4.5rem,10vw,8rem)]">
      <Container className="grid items-start gap-[clamp(1.5rem,5vw,4rem)] lg:grid-cols-2">
        <div>
          <SectionHeading id="t-faq" eyebrow="03 — FAQ">
            Perguntas frequentes
          </SectionHeading>
          <p className="text-muted-foreground mt-4 max-w-[36ch]">
            Não encontras a resposta?{' '}
            <a
              href="#contacto"
              className="text-primary hover:text-foreground underline-offset-4 hover:underline"
            >
              Fala connosco
            </a>
            .
          </p>
        </div>

        <div className="border-t">
          {items.map((item, index) => (
            <details key={item.id} name="faq" open={index === 0} className="group border-b">
              <summary className="hover:text-primary flex min-h-16 cursor-pointer list-none items-center justify-between gap-4 py-4 text-base leading-snug font-semibold transition-colors [&::-webkit-details-marker]:hidden">
                <h3>{item.question}</h3>
                <Plus
                  aria-hidden="true"
                  className="text-muted-foreground ease-brand size-[18px] flex-none transition-transform duration-200 group-open:rotate-45"
                />
              </summary>
              <p className="text-muted-foreground pr-10 pb-5 text-base text-pretty">
                {item.answer}
              </p>
            </details>
          ))}
        </div>
      </Container>
    </section>
  );
}
