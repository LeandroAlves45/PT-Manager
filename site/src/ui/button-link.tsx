import type { ComponentProps } from 'react';
import { cva, type VariantProps } from 'class-variance-authority';

import { cn } from '@/ui/cn';

/**
 * Variantes de CTA, derivadas do `Button` do shadcn do frontend.
 *
 * No site todos os CTAs navegam (âncoras ou a app), por isso o elemento é sempre `<a>`:
 * um `<button>` a navegar teria a semântica errada para leitores de ecrã.
 * Alturas ≥ 44px (alvo de toque em mobile).
 */
export const buttonLinkVariants = cva(
  'ease-brand inline-flex shrink-0 items-center justify-center gap-2 rounded-xl font-semibold whitespace-nowrap transition-[translate,filter,background-color,border-color] duration-200 [&_svg]:pointer-events-none [&_svg]:shrink-0',
  {
    variants: {
      variant: {
        primary:
          'glow-primary bg-primary text-primary-foreground hover:-translate-y-px hover:brightness-110',
        outline:
          'border-border-strong bg-card/60 text-foreground hover:border-muted-foreground/50 hover:bg-surface border',
        ghost: 'text-foreground hover:bg-foreground/5',
      },
      size: {
        sm: 'h-11 px-4 text-sm',
        lg: 'h-12 px-6 text-base',
        block: 'h-12 w-full px-6 text-base',
      },
    },
    defaultVariants: { variant: 'primary', size: 'sm' },
  }
);

export type ButtonLinkProps = ComponentProps<'a'> & VariantProps<typeof buttonLinkVariants>;

export function ButtonLink({ className, variant, size, ...props }: ButtonLinkProps) {
  return <a className={cn(buttonLinkVariants({ variant, size }), className)} {...props} />;
}
