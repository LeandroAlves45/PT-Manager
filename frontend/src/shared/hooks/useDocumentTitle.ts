import { useEffect } from 'react';

/**
 * Título do separador com o nome da app do personal trainer, enquanto o componente estiver
 * montado. Repõe o título anterior ao sair. Sem nome (marca por carregar ou vazia) não mexe.
 */
export function useDocumentTitle(appName: string | undefined) {
  useEffect(() => {
    const name = appName?.trim();
    if (name === undefined || name === '') return;

    const previous = document.title;
    document.title = name;
    return () => {
      document.title = previous;
    };
  }, [appName]);
}
