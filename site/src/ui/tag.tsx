import type { ReactNode } from 'react';

import { cn } from '@/ui/cn';

interface TagProps {
  children: ReactNode;
  tone?: 'neutral' | 'primary' | 'solid';
  className?: string;
}

/** Etiqueta mono em maiúsculas (derivada do `Badge` do shadcn, no estilo do design). */
export function Tag({ children, tone = 'neutral', className }: TagProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-md px-2 py-1 font-mono text-xs font-medium tracking-[0.08em] uppercase',
        tone === 'neutral' && 'border-border-strong text-muted-foreground border',
        tone === 'primary' && 'border-primary-line text-primary border',
        tone === 'solid' && 'bg-primary text-primary-foreground',
        className
      )}
    >
      {children}
    </span>
  );
}
