import { useEffect, useRef, type ReactNode } from 'react';

/**
 * Título (e descrição) de um ecrã de autenticação.
 *
 * Com `autoFocus` o título recebe o foco ao montar: quando o conteúdo muda sem navegação
 * (formulário que passa a "Verifica o teu email", resultado de uma confirmação) o foco ficava
 * no botão que desapareceu e o leitor de ecrã não anunciava nada. `tabIndex={-1}` torna-o
 * focável por código sem o pôr na ordem do Tab.
 */
export function AuthHeading({
  title,
  description,
  autoFocus = false,
}: {
  title: string;
  description?: ReactNode;
  autoFocus?: boolean;
}) {
  const ref = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    if (autoFocus) ref.current?.focus();
  }, [autoFocus]);

  return (
    <header className="space-y-1">
      <h1 ref={ref} tabIndex={-1} className="font-display text-2xl leading-none outline-none">
        {title}
      </h1>
      {description !== undefined && <p className="text-muted-foreground text-sm">{description}</p>}
    </header>
  );
}
