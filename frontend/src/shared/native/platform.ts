import { Capacitor } from '@capacitor/core';

/**
 * Indica se a SPA corre dentro da app Android ou iOS (Capacitor).
 *
 * Único ponto do código que pergunta ao Capacitor: tudo o que muda na app (sessão por token,
 * Google escondido, deep links) decide a partir daqui, e os testes simulam só este módulo.
 */
export function isNativeApp(): boolean {
  return Capacitor.isNativePlatform();
}
