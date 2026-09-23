import { useCallback, useSyncExternalStore } from 'react';

/**
 * Observa uma media query.
 *
 * Usa `useSyncExternalStore` em vez de `useState` com `useEffect` porque a media query é
 * exactamente isso: uma fonte de verdade externa ao React. A leitura é síncrona, o que
 * evita um primeiro fotograma com o layout errado (a sidebar a aparecer e a desaparecer),
 * e não há `setState` dentro de efeitos a provocar renders em cascata.
 *
 * @param query Media query CSS, ex.: `(min-width: 768px)`.
 */
export function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onStoreChange: () => void) => {
      const mediaQuery = window.matchMedia(query);
      mediaQuery.addEventListener('change', onStoreChange);
      return () => mediaQuery.removeEventListener('change', onStoreChange);
    },
    [query]
  );

  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);

  // Sem viewport (renderização fora do browser) assume-se "não corresponde", o que
  // mantém o layout mobile-first.
  return useSyncExternalStore(subscribe, getSnapshot, () => false);
}
