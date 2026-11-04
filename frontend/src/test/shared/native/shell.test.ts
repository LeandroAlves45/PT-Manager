import { beforeEach, describe, expect, it, vi } from 'vitest';

import { initNativeShell, openExternalUrl } from '@/shared/native/shell';

type Listener = (event: { url?: string; canGoBack?: boolean }) => void;

const app = vi.hoisted(() => ({
  listeners: new Map<string, (event: unknown) => void>(),
  launchUrl: undefined as string | undefined,
  exitApp: vi.fn(() => Promise.resolve()),
  browserOpen: vi.fn<(options: { url: string }) => Promise<void>>(() => Promise.resolve()),
}));

vi.mock('@capacitor/app', () => ({
  App: {
    addListener: (name: string, listener: (event: unknown) => void) => {
      app.listeners.set(name, listener);
      return Promise.resolve({ remove: () => Promise.resolve() });
    },
    getLaunchUrl: () =>
      Promise.resolve(app.launchUrl === undefined ? undefined : { url: app.launchUrl }),
    exitApp: app.exitApp,
  },
}));
vi.mock('@capacitor/browser', () => ({ Browser: { open: app.browserOpen } }));

const TOKEN = 'AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_abcde';

function fire(name: string, event: Parameters<Listener>[0]): void {
  const listener = app.listeners.get(name);
  if (listener === undefined) throw new Error(`listener ${name} not registered`);
  listener(event);
}

beforeEach(() => {
  app.listeners.clear();
  app.launchUrl = undefined;
  app.exitApp.mockClear();
  app.browserOpen.mockClear();
});

describe('initNativeShell', () => {
  it('opens the invitation when the app was started by the email link', async () => {
    app.launchUrl = `https://app.ptmanager.pt/aceitar-convite?token=${TOKEN}`;
    const navigate = vi.fn();

    await initNativeShell({ navigate });

    expect(navigate).toHaveBeenCalledExactlyOnceWith(`/aceitar-convite?token=${TOKEN}`);
  });

  it('opens a link received while the app is already open, and ignores a foreign one', async () => {
    const navigate = vi.fn();
    await initNativeShell({ navigate });

    fire('appUrlOpen', { url: `https://evil.test/repor-password?token=${TOKEN}` });
    fire('appUrlOpen', { url: `https://app.ptmanager.pt/repor-password?token=${TOKEN}` });

    expect(navigate).toHaveBeenCalledExactlyOnceWith(`/repor-password?token=${TOKEN}`);
  });

  it('goes back in history with the Android back button, and closes the app on the first page', async () => {
    const back = vi.spyOn(window.history, 'back').mockImplementation(() => undefined);
    await initNativeShell({ navigate: vi.fn() });

    fire('backButton', { canGoBack: true });
    expect(back).toHaveBeenCalledOnce();
    expect(app.exitApp).not.toHaveBeenCalled();

    fire('backButton', { canGoBack: false });
    expect(app.exitApp).toHaveBeenCalledOnce();
  });
});

describe('openExternalUrl', () => {
  it('opens the address in the system browser', async () => {
    await openExternalUrl('https://www.youtube.com/watch?v=abc');

    expect(app.browserOpen).toHaveBeenCalledExactlyOnceWith({
      url: 'https://www.youtube.com/watch?v=abc',
    });
  });
});
