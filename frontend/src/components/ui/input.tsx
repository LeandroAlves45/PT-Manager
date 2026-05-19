import * as React from 'react';
import { cn } from '@/lib/utils';

const Input = React.forwardRef<
  HTMLInputElement, // Tipo do elemento que o ref aponta
  React.ComponentPropsWithoutRef<'input'> // Todas as props válidas de um <input>
>(({ className, type, ...props }, ref) => {
  return (
    <input
      type={type} // Tipo do input (text, password, email, etc.)
      className={cn(
        // Classes base do componente
        'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
        className // Permite extender classes externamente
      )}
      ref={ref} // Forward do ref corretamente tipado
      {...props} // Spread de todas as outras props do <input>
    />
  );
});

Input.displayName = 'Input';

export { Input };
