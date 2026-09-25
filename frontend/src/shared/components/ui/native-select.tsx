import * as React from 'react';

import { cn } from '@/shared/lib/utils';

/**
 * `<select>` nativo com o mesmo aspeto do `Input`.
 *
 * Só para listas curtas e fixas (sexo, nível de atividade): acessível, funciona com o
 * teclado e o seletor do telemóvel sem código extra. Catálogos longos usam o `Combobox`.
 */
function NativeSelect({ className, ...props }: React.ComponentProps<'select'>) {
  return (
    <select
      data-slot="native-select"
      className={cn(
        'border-input bg-background dark:bg-input/30 h-9 w-full min-w-0 rounded-md border px-3 py-1 text-base shadow-xs transition-[color,box-shadow] outline-none disabled:cursor-not-allowed disabled:opacity-50 md:text-sm',
        'focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]',
        'aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40',
        className
      )}
      {...props}
    />
  );
}

export { NativeSelect };
