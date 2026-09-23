import { useContext } from 'react';

import { ThemeContext, type ThemeContextValue } from '@/app/providers/theme-context';
import { invariant } from '@/shared/lib/invariant';

/** Lê o tema atual. Só funciona dentro de 'ThemeProvider'. */
export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);
  invariant(context, 'useTheme foi usado fora de <ThemeProvider>.');
  return context;
}
