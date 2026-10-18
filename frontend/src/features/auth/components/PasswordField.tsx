import { Eye, EyeOff } from 'lucide-react';
import { useId, useState, type ComponentProps } from 'react';

import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';

type PasswordFieldProps = Omit<ComponentProps<'input'>, 'type' | 'id' | 'autoComplete'> & {
  label: string;
  /** Mensagem de erro do campo, ligada por `aria-describedby`. */
  error?: string | undefined;
  /** Regra ou ajuda mostrada por baixo do campo (a regra de composição da palavra-passe). */
  hint?: string;
  /** `current-password` ao entrar; `new-password` ao criar ou mudar (não preenche a antiga). */
  autoComplete: 'current-password' | 'new-password';
};

/**
 * Campo de palavra-passe com botão de mostrar/ocultar.
 *
 * O botão é um alternador (`aria-pressed`) com nome fixo: mudar o nome ao mesmo tempo que o
 * estado faria o leitor de ecrã anunciar o contrário do que acontece. O `ref` e os eventos do
 * react-hook-form chegam ao `<input>` pelo spread (`{...register('password')}`).
 */
export function PasswordField({
  label,
  error,
  hint,
  autoComplete,
  ...inputProps
}: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);
  const hintId = useId();

  return (
    <FormField label={label} error={error}>
      {(control) => (
        <>
          <div className="relative">
            <Input
              {...inputProps}
              {...control}
              type={visible ? 'text' : 'password'}
              autoComplete={autoComplete}
              aria-describedby={
                [control['aria-describedby'], hint === undefined ? undefined : hintId]
                  .filter((id) => id !== undefined)
                  .join(' ') || undefined
              }
              className="h-11 pr-11"
            />
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-pressed={visible}
              aria-label={`Mostrar ${label.toLowerCase()}`}
              onClick={() => setVisible((value) => !value)}
              className="absolute top-1.5 right-1.5"
            >
              {visible ? <EyeOff aria-hidden /> : <Eye aria-hidden />}
            </Button>
          </div>
          {hint !== undefined && (
            <span id={hintId} className="text-muted-foreground text-xs">
              {hint}
            </span>
          )}
        </>
      )}
    </FormField>
  );
}
