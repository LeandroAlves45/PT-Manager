import { useId, type ReactNode } from 'react';

import { cn } from '@/shared/lib/utils';

/** Atributos que o 'FormField' entrega ao controlo para o ligar ao rótulo e ao erro. */
export interface FieldControlProps {
  readonly id: string;
  readonly 'aria-invalid': boolean;
  readonly 'aria-describedby': string | undefined;
}

/**
 * Rótulo, controlo e mensagem de erro de um campo de formulário.
 *
 * O rótulo liga-se por `htmlFor` e o erro por `aria-describedby`, fora do `<label>`: se o
 * erro (ou as `<option>` de um `<select>`) ficassem dentro do rótulo, entravam no nome
 * acessível do campo e o leitor de ecrã anunciava "Peso (kg) Indica o peso." como nome.
 *
 * @param hint Texto de ajuda opcional, por baixo do controlo e anunciado com ele.
 * @param children Função que recebe os atributos de ligação e devolve o controlo.
 */
export function FormField({
  label,
  hint,
  error,
  className,
  children,
}: {
  label: string;
  hint?: string;
  error?: string | undefined;
  className?: string;
  children: (control: FieldControlProps) => ReactNode;
}) {
  const id = useId();
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const describedBy =
    [hint !== undefined ? hintId : null, error !== undefined ? errorId : null]
      .filter((value) => value !== null)
      .join(' ') || undefined;

  return (
    <div className={cn('flex flex-col gap-1 text-sm', className)}>
      <label htmlFor={id}>{label}</label>
      {children({
        id,
        'aria-invalid': error !== undefined,
        'aria-describedby': describedBy,
      })}
      {hint !== undefined && (
        <span id={hintId} className="text-muted-foreground text-xs">
          {hint}
        </span>
      )}
      {error !== undefined && (
        <span id={errorId} role="alert" className="text-destructive">
          {error}
        </span>
      )}
    </div>
  );
}
