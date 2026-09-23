import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';

import {
  THEME_STORAGE_KEY,
  ThemeContext,
  type ResolvedTheme,
  type ThemePreference,
} from '@/app/providers/theme-context';
import { useMediaQuery } from '@/shared/hooks/useMediaQuery';

const PREFERENCES: readonly ThemePreference[] = ['light', 'dark', 'system'];

/**
 * Lê a preferência guardada, tolerando armazenamento bloqueado.
 *
 * O acesso ao `localStorage` lança em janelas privadas e com cookies de terceiros
 * desligados; nesse caso o tema simplesmente volta a `system`.
 */
function readStoredPreference(): ThemePreference {
  try {
    // eslint-disable-next-line no-restricted-globals -- preferência de tema, não é dado de sessão
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    return PREFERENCES.includes(stored as ThemePreference) ? (stored as ThemePreference) : 'system';
  } catch {
    return 'system';
  }
}

/**
 * Aplica o tema ao documento e recorda a escolha do utilizador.
 *
 * O tema escuro é o principal do produto: a classe `dark` já vem no `index.html` para
 * não haver um clarão branco antes de o React arrancar.
 *
 * `resolved` é **derivado**, não estado: guardá-lo obrigaria a sincronizá-lo num efeito
 * sempre que a preferência ou o tema do sistema mudassem, que é exactamente o padrão de
 * renders em cascata que o React desaconselha.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredPreference());
  const systemPrefersDark = useMediaQuery('(prefers-color-scheme: dark)');

  const resolved: ResolvedTheme =
    preference === 'system' ? (systemPrefersDark ? 'dark' : 'light') : preference;

  // Escrever no '<html>' é sincronizar com um sistema externo - o sítio certo para o efeito.
  useEffect(() => {
    document.documentElement.classList.toggle('dark', resolved === 'dark');
    document.documentElement.style.colorScheme = resolved;
  }, [resolved]);

  const setPreference = useCallback((next: ThemePreference) => {
    setPreferenceState(next);
    try {
      // eslint-disable-next-line no-restricted-globals -- preferência de tema, não é dado de sessão
      localStorage.setItem(THEME_STORAGE_KEY, next);
    } catch {
      // Sem armazenamento a escolha vale só para esta sessão do browser.
    }
  }, []);

  const value = useMemo(
    () => ({ preference, resolved, setPreference }),
    [preference, resolved, setPreference]
  );

  return <ThemeContext value={value}>{children}</ThemeContext>;
}
