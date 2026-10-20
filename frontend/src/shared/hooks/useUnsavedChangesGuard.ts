import { useEffect } from 'react';
import { useBlocker } from 'react-router';

/**
 * Impede sair de um formulário com alterações por guardar.
 *
 * Bloqueia a navegação dentro da aplicação (`useBlocker`) e avisa ao fechar ou recarregar o
 * separador (`beforeunload`). Quem chama mostra o diálogo quando `blocker.state === 'blocked'`
 * e decide com `proceed()` ou `reset()`.
 *
 * @param when `true` enquanto há alterações por guardar; passar `false` logo que o guardar
 *   tem sucesso, para a navegação seguinte não ser bloqueada.
 */
export function useUnsavedChangesGuard(when: boolean) {
  const blocker = useBlocker(when);

  useEffect(() => {
    if (!when) return;

    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [when]);

  return blocker;
}
