import { useRef, useState } from 'react';
import { Palette } from 'lucide-react';

import type { BrandSwatch } from '@/domain/types';
import { cn } from '@/ui/cn';
import { Tag } from '@/ui/tag';

/**
 * Classes de cada amostra. Têm de ser literais para o Tailwind as gerar; um teste garante
 * que cada classe corresponde ao `value` de `content/brand.ts`.
 */
export const swatchClass: Record<BrandSwatch['id'], string> = {
  blue: 'bg-[#00a3e9]',
  orange: 'bg-[#fb923c]',
  green: 'bg-[#4ade80]',
  pink: 'bg-[#f472b6]',
  violet: 'bg-[#c4b5fd]',
};

interface BrandShowcaseProps {
  swatches: readonly BrandSwatch[];
}

/**
 * Cartão de destaque "marca própria" com uma demonstração interativa de cor.
 *
 * A cor escolhida é aplicada com `style.setProperty('--demo-brand', …)` (CSSOM). A CSP
 * `style-src 'self'` bloqueia atributos `style` no HTML, mas não alterações via CSSOM.
 */
export function BrandShowcase({ swatches }: BrandShowcaseProps) {
  const previewRef = useRef<HTMLDivElement>(null);
  const [selected, setSelected] = useState<BrandSwatch['id'] | undefined>(swatches[0]?.id);

  const pick = (swatch: BrandSwatch) => {
    setSelected(swatch.id);
    previewRef.current?.style.setProperty('--demo-brand', swatch.value);
  };

  return (
    <article className="bg-card border-primary-line relative flex flex-col gap-5 overflow-hidden rounded-2xl border p-[clamp(1.25rem,3vw,2rem)] md:col-span-2 lg:col-span-4 lg:row-span-2">
      <div
        aria-hidden="true"
        className="card-halo pointer-events-none absolute -right-[20%] -bottom-[40%] h-4/5 w-[70%]"
      />

      <div className="flex flex-wrap items-center gap-3">
        <span className="border-primary-line text-primary inline-flex size-10 items-center justify-center rounded-lg border">
          <Palette aria-hidden="true" className="size-5" strokeWidth={1.8} />
        </span>
        <Tag tone="solid">Incluído desde o Free</Tag>
      </div>

      <div>
        <h3 className="text-[clamp(1.375rem,2.4vw,1.75rem)] font-semibold tracking-tight">
          A tua marca no portal do cliente
        </h3>
        <p className="text-muted-foreground mt-2 max-w-[46ch]">
          Logo e cor próprias desde o primeiro dia, em todos os planos. Os teus clientes veem o teu
          negócio.
        </p>
      </div>

      <div
        ref={previewRef}
        className="relative mt-auto grid items-end gap-4 sm:grid-cols-[1.4fr_1fr]"
      >
        <div aria-hidden="true" className="bg-background overflow-hidden rounded-xl border">
          <div className="bg-demo-brand ease-brand flex h-11 items-center gap-2 px-3 transition-colors duration-200">
            <span className="bg-primary-foreground/85 h-[18px] w-16 rounded" />
            <span className="bg-primary-foreground/35 ml-auto size-[22px] rounded-full" />
          </div>
          <div className="flex flex-col gap-2.5 p-3.5">
            <span className="bg-placeholder h-2 w-1/2 rounded" />
            <span className="bg-muted block h-1.5 w-full overflow-hidden rounded-sm">
              <span className="bg-demo-brand ease-brand block h-full w-[64%] transition-colors duration-200" />
            </span>
            <span className="bg-placeholder h-2 w-[72%] rounded" />
            <span className="bg-demo-brand ease-brand h-7 w-24 self-start rounded-lg transition-colors duration-200" />
          </div>
        </div>

        <fieldset>
          <legend className="text-muted-foreground mb-2.5 text-xs">Experimenta uma cor</legend>
          <div className="flex flex-wrap gap-2">
            {swatches.map((swatch) => (
              <button
                key={swatch.id}
                type="button"
                onClick={() => pick(swatch)}
                aria-pressed={selected === swatch.id}
                aria-label={`Cor de exemplo: ${swatch.label}`}
                className={cn(
                  'border-card ease-brand size-11 cursor-pointer rounded-full border-2 transition-transform duration-150 hover:scale-105',
                  swatchClass[swatch.id],
                  selected === swatch.id ? 'ring-foreground ring-2' : 'ring-border-strong ring-1'
                )}
              />
            ))}
          </div>
        </fieldset>
      </div>
    </article>
  );
}
