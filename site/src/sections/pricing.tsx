import { clientLimitLabel } from '@/domain/plan';
import type { Plan } from '@/domain/types';
import { ButtonLink } from '@/ui/button-link';
import { CheckIcon } from '@/ui/check-icon';
import { cn } from '@/ui/cn';
import { Container } from '@/ui/container';
import { SectionHeading } from '@/ui/section-heading';
import { Tag } from '@/ui/tag';

function PricingCard({ plan, signupUrl }: { plan: Plan; signupUrl: string }) {
  const titleId = `plano-${plan.code.toLowerCase()}`;
  return (
    <article
      aria-labelledby={titleId}
      className={cn(
        'flex flex-col gap-6 rounded-2xl p-7',
        plan.highlighted ? 'pro-gradient glow-primary' : 'bg-card border'
      )}
    >
      <div>
        <div className="flex items-center justify-between gap-3">
          <h3 id={titleId} className="font-display text-[1.75rem] tracking-[0.02em]">
            {plan.code}
          </h3>
          {plan.badge && <Tag tone="solid">{plan.badge}</Tag>}
        </div>
        <p className="mt-3.5 flex items-baseline gap-2">
          <span className="tabular text-[2.75rem] font-medium tracking-tight">
            {plan.monthlyPriceEur}€
          </span>
          <span className="text-muted-foreground text-sm">{plan.priceNote}</span>
        </p>
      </div>

      <ul className="flex flex-col gap-3 text-base">
        {[clientLimitLabel(plan), ...plan.benefits].map((benefit) => (
          <li key={benefit} className="flex items-start gap-2.5">
            <CheckIcon />
            {benefit}
          </li>
        ))}
      </ul>

      <ButtonLink
        href={signupUrl}
        variant={plan.highlighted ? 'primary' : 'outline'}
        size="block"
        className="mt-auto"
        aria-label={`Criar conta grátis — plano ${plan.code}`}
      >
        Criar conta grátis
      </ButtonLink>
    </article>
  );
}

interface PricingProps {
  plans: readonly Plan[];
  signupUrl: string;
}

export function Pricing({ plans, signupUrl }: PricingProps) {
  return (
    <section id="planos" aria-labelledby="t-planos" className="pt-[clamp(4.5rem,10vw,8rem)]">
      <Container>
        <SectionHeading id="t-planos" eyebrow="02 — Planos" className="[&_h2]:max-w-none">
          Começa grátis. Cresce quando precisares.
        </SectionHeading>
        <div className="mt-12 grid items-stretch gap-3 md:grid-cols-3">
          {plans.map((plan) => (
            <PricingCard key={plan.code} plan={plan} signupUrl={signupUrl} />
          ))}
        </div>
      </Container>
    </section>
  );
}
