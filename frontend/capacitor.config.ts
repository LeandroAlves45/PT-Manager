import type { CapacitorConfig } from '@capacitor/cli';

/**
 * App PT Manager para Android e iOS (v1: só o portal do cliente).
 *
 * A SPA vai empacotada na app (`webDir`), sem `server.url`: o Capacitor não recomenda carregar
 * um site remoto em produção, e a Apple recusa apps que são só um site embrulhado.
 *
 * Origens da WebView: `https://localhost` no Android (`androidScheme`) e `capacitor://localhost`
 * no iOS. São as duas origens da política CORS `NativeApp` do backend, sem credenciais.
 */
const config: CapacitorConfig = {
  appId: 'pt.ptmanager.app',
  appName: 'PT Manager',
  webDir: 'dist',
  android: {
    // Sem rede em claro: a API é sempre HTTPS.
    allowMixedContent: false,
  },
  server: {
    androidScheme: 'https',
  },
  plugins: {
    SystemBars: {
      // A WebView recebe as margens do sistema em env(safe-area-inset-*) (e em variáveis CSS
      // nas WebViews Android antigas), como no Safari do iPhone.
      insetsHandling: 'css',
      initialViewportFitValueHint: 'cover',
    },
    SplashScreen: {
      launchShowDuration: 0,
      launchAutoHide: true,
      backgroundColor: '#05080C',
    },
  },
};

export default config;
