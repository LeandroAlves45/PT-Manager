import { createContext } from 'react';

/** Preferência de tema escolhida pelo utilizador. */
export type ThemePreference = 'light' | 'dark' | 'system';

/** Tema efetivamente aplicado ao documento depois de resolver system. */
export type ResolvedTheme = 'light' | 'dark';

export interface ThemeContextValue {
  readonly preference: ThemePreference;
  readonly resolved: ResolvedTheme;
  readonly setPreference: (preference: ThemePreference) => void;
}

export const ThemeContext = createContext<ThemeContextValue | null>(null);

/** Chave usada para recordar a preferência entre visitas. */
export const THEME_STORAGE_KEY = 'pt-manager.theme';
