import { useId, type ReactNode } from 'react';

import { cn } from '@/shared/lib/utils';

/** Atributos que o 'FormField' entrega ao controlo para o ligar ao rótulo e ao erro. */
export interface FieldControlProps {
  readonly id: string;
  readonly 'aria-invalid': boolean;
  readonly 'aria-describedby': string | undefined;
}

/**
 * Rótulo, controlo e mensagem de erro de um campo dos formulários de clientes.
 *
 * O rótulo liga-se por `htmlFor` e o erro por `aria-describedby`, fora do `<label>`: se o
 * erro (ou as `<option>` de um `<select>`) ficassem dentro do rótulo, entravam no nome
 * acessível do campo e o leitor de ecrã anunciava "Peso (kg) Indica o peso." como nome.
 *
 * @param children Função que recebe os atributos de ligação e devolve o controlo.
 */
export function FormField({
  label,
  error,
  className,
  children,
}: {
  label: string;
  error?: string | undefined;
  className?: string;
  children: (control: FieldControlProps) => ReactNode;
}) {
  const id = useId();
  const errorId = `${id}-error`;

  return (
    <div className={cn('flex flex-col gap-1 text-sm', className)}>
      <label htmlFor={id}>{label}</label>
      {children({
        id,
        'aria-invalid': error !== undefined,
        'aria-describedby': error !== undefined ? errorId : undefined,
      })}
      {error !== undefined && (
        <span id={errorId} role="alert" className="text-destructive">
          {error}
        </span>
      )}
    </div>
  );
}
