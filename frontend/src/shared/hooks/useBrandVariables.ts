import { useEffect } from 'react';

import type { BrandPair } from '@/shared/lib/brandTheme';

/** Variáveis de `globals.css` que a cor principal do personal trainer substitui. */
const BRAND_VARIABLES = ['--primary', '--primary-foreground', '--ring'] as const;

/**
 * Aplica a cor principal do personal trainer ao documento inteiro enquanto o componente está
 * montado: no portal do cliente e na área do personal trainer.
 *
 * Vai para `document.documentElement` e não para um `div`: diálogos, menus e toasts são
 * renderizados em portais fora da árvore do layout e têm de herdar a mesma cor. O estilo
 * inline ganha a `:root` e a `.dark`; ao desmontar (logout, troca de papel) as propriedades
 * são removidas e os tokens PT Manager voltam.
 *
 * @param primary Par já validado para AA por `resolveBrand`, ou `null` para manter o tema.
 */
export function useBrandVariables(primary: BrandPair | null): void {
  const background = primary?.background ?? null;
  const foreground = primary?.foreground ?? null;

  useEffect(() => {
    if (background === null || foreground === null) return;

    const style = document.documentElement.style;
    style.setProperty('--primary', background);
    style.setProperty('--primary-foreground', foreground);
    style.setProperty('--ring', background);

    return () => {
      for (const name of BRAND_VARIABLES) style.removeProperty(name);
    };
  }, [background, foreground]);
}
