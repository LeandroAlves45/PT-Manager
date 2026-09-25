import { ArrowRight } from 'lucide-react';

import { DashboardPreview } from '@/sections/dashboard-preview';
import { ButtonLink } from '@/ui/button-link';
import { Container } from '@/ui/container';

interface HeroProps {
  signupUrl: string;
  /** Nota de rodapé do CTA, derivada do plano gratuito. */
  freePlanNote: string;
}

export function Hero({ signupUrl, freePlanNote }: HeroProps) {
  return (
    <section id="topo" aria-labelledby="hero-title" className="relative overflow-hidden border-b">
      <div aria-hidden="true" className="hero-halo pointer-events-none absolute inset-0" />
      <div aria-hidden="true" className="hero-grid pointer-events-none absolute inset-0" />

      <Container className="relative pt-[clamp(3.5rem,10vw,7.5rem)]">
        <p className="bg-card/70 text-muted-foreground inline-flex items-center gap-2 rounded-full border py-1.5 pr-3 pl-2 text-sm">
          <span aria-hidden="true" className="bg-primary size-2 rounded-full" />
          Para personal trainers e nutricionistas
        </p>

        <h1
          id="hero-title"
          className="font-display mt-6 max-w-[14ch] text-[clamp(2.875rem,8.6vw,7rem)] leading-[0.9] text-balance uppercase"
        >
          Clientes, treino e nutrição. <span className="text-primary">Num só lugar.</span>
        </h1>

        <p className="text-muted-foreground mt-6 max-w-[56ch] text-[clamp(1.0625rem,1.6vw,1.1875rem)] text-pretty">
          O PT Manager junta os planos de treino, nutrição e suplementação, os check-ins e as
          sessões dos teus clientes numa única plataforma — com a tua marca no portal do cliente.
        </p>

        <div className="mt-8 flex flex-wrap gap-3">
          <ButtonLink href={signupUrl} size="lg">
            Criar conta grátis
            <ArrowRight aria-hidden="true" className="size-4" />
          </ButtonLink>
          <ButtonLink href="#planos" variant="outline" size="lg">
            Ver planos
          </ButtonLink>
        </div>
        <p className="text-muted-foreground mt-3.5 text-sm">{freePlanNote}</p>

        <DashboardPreview />
      </Container>
    </section>
  );
}
