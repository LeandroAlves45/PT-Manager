import { useEffect, useState } from "react";

/**
 * Devolve o valor só depois de ele estar estável durante `delayMs`.
 *
 * Usado na pesquisa da command palette e nos filtros de listagem: sem isto, cada tecla
 * premida seria um pedido à API.
 *
 * @param value Valor a atrasar.
 * @param delayMs Tempo de estabilidade exigido; 300 ms é o valor do desenho aprovado.
 */
export function useDebounce<T>(value: T, delayMs: number = 300): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
