import { resolveDeepLink } from '@/shared/native/deep-links';

/** O que a casca nativa precisa do router: navegar para uma rota interna. */
export interface NativeNavigator {
  navigate: (to: string) => unknown;
}

/**
 * Liga a SPA aos eventos da app nativa. Chamado uma vez no arranque, só dentro da app.
 *
 * - Links dos emails (convite, reposição da palavra-passe): abrem a rota certa, tanto com a
 *   app fechada (`getLaunchUrl`) como já aberta (`appUrlOpen`).
 * - Botão "voltar" do Android: recua no histórico; na primeira página, fecha a app em vez de
 *   não fazer nada.
 */
export async function initNativeShell(navigator: NativeNavigator): Promise<void> {
  const { App } = await import('@capacitor/app');

  const open = (url: string | undefined): void => {
    if (url === undefined) return;
    const route = resolveDeepLink(url);
    if (route !== null) void navigator.navigate(route);
  };

  await App.addListener('appUrlOpen', (event) => open(event.url));
  await App.addListener('backButton', (event) => {
    if (event.canGoBack) window.history.back();
    else void App.exitApp();
  });

  open((await App.getLaunchUrl())?.url);
}

/**
 * Abre um endereço externo (YouTube, Vimeo) no browser do sistema. Dentro da WebView um
 * `target="_blank"` não abre nada; fora da app não é chamado.
 */
export async function openExternalUrl(url: string): Promise<void> {
  const { Browser } = await import('@capacitor/browser');
  await Browser.open({ url });
}
