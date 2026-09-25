import type { ComponentProps } from 'react';

import { cn } from '@/ui/cn';

/** Largura máxima e margens laterais comuns a todas as secções (16px no telemóvel). */
export function Container({ className, ...props }: ComponentProps<'div'>) {
  return (
    <div className={cn('mx-auto w-full max-w-300 px-4 sm:px-6 lg:px-8', className)} {...props} />
  );
}
