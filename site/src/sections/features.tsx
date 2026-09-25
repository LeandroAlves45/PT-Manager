import {
  Activity,
  ClipboardList,
  Clock,
  Dumbbell,
  PieChart,
  Pill,
  Users,
  type LucideIcon,
} from 'lucide-react';

import type { BrandSwatch, Feature, FeatureIcon } from '@/domain/types';
import { BrandShowcase } from '@/sections/brand-showcase';
import { cn } from '@/ui/cn';
import { Container } from '@/ui/container';
import { SectionHeading } from '@/ui/section-heading';
import { Tag } from '@/ui/tag';

const icons: Record<FeatureIcon, LucideIcon> = {
  nutrition: PieChart,
  training: Dumbbell,
  clients: Users,
  supplements: Pill,
  checkins: Activity,
  assessments: ClipboardList,
  sessions: Clock,
};

/** Grelha bento de 6 colunas no desktop; 2 no tablet; 1 no telemóvel. */
const spanClass: Record<Feature['span'], string> = {
  third: 'lg:col-span-2',
  half: 'lg:col-span-3',
};

function FeatureCard({ feature }: { feature: Feature }) {
  const Icon = icons[feature.icon];
  return (
    <article
      className={cn(
        // O último cartão ocupa a linha inteira no tablet (grelha de 2 com número ímpar de cartões).
        'bg-card hover:border-border-strong ease-brand flex flex-col gap-3.5 rounded-2xl border p-6 transition-colors duration-200 md:max-lg:last:col-span-2',
        spanClass[feature.span]
      )}
    >
      <span className="border-border-strong text-primary inline-flex size-10 items-center justify-center rounded-lg border">
        <Icon aria-hidden="true" className="size-5" strokeWidth={1.8} />
      </span>
      <div>
        <h3 className="text-lg font-semibold">{feature.title}</h3>
        <p className="text-muted-foreground mt-1.5 text-base">{feature.description}</p>
      </div>
      {feature.tags && (
        <ul className="mt-auto flex flex-wrap gap-1.5" aria-label={`Destaques: ${feature.title}`}>
          {feature.tags.map((tag) => (
            <li key={tag.label}>
              <Tag tone={tag.highlight ? 'primary' : 'neutral'}>{tag.label}</Tag>
            </li>
          ))}
        </ul>
      )}
    </article>
  );
}

interface FeaturesProps {
  features: readonly Feature[];
  swatches: readonly BrandSwatch[];
}

export function Features({ features, swatches }: FeaturesProps) {
  return (
    <section id="funcionalidades" aria-labelledby="t-func" className="pt-[clamp(4.5rem,10vw,8rem)]">
      <Container>
        <SectionHeading id="t-func" eyebrow="01 — Funcionalidades">
          Tudo o que usas com os teus clientes, sem folhas de cálculo.
        </SectionHeading>

        <div className="mt-12 grid grid-flow-dense grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-6">
          <BrandShowcase swatches={swatches} />
          {features.map((feature) => (
            <FeatureCard key={feature.id} feature={feature} />
          ))}
        </div>
      </Container>
    </section>
  );
}
