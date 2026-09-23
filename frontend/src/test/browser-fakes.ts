import { vi } from 'vitest';

/**
 * Substitutos controláveis das APIs de browser que o jsdom não tem.
 *
 * Só os testes que precisam delas as instalam; `setup.ts` desfaz os `stubGlobal` e o
 * `uninstallWebLocks` tem de ser chamado por quem instalou o lock.
 */

/**
 * `BroadcastChannel` em memória: entrega a todas as outras instâncias com o mesmo nome e
 * guarda cada mensagem publicada para o teste poder inspecionar o payload.
 */
export class FakeBroadcastChannel extends EventTarget {
  static readonly instances: FakeBroadcastChannel[] = [];
  static readonly published: unknown[] = [];

  readonly name: string;

  constructor(name: string) {
    super();
    this.name = name;
    FakeBroadcastChannel.instances.push(this);
  }

  postMessage(data: unknown): void {
    FakeBroadcastChannel.published.push(structuredClone(data));
    for (const other of FakeBroadcastChannel.instances) {
      if (other !== this && other.name === this.name) {
        other.dispatchEvent(new MessageEvent('message', { data: structuredClone(data) }));
      }
    }
  }

  close(): void {
    const index = FakeBroadcastChannel.instances.indexOf(this);
    if (index >= 0) FakeBroadcastChannel.instances.splice(index, 1);
  }
}

/**
 * Instala o canal falso. As instâncias existentes são mantidas de propósito: o módulo de
 * eventos guarda o seu canal num singleton que sobrevive entre testes do mesmo ficheiro.
 */
export function installBroadcastChannel(): void {
  FakeBroadcastChannel.published.length = 0;
  vi.stubGlobal('BroadcastChannel', FakeBroadcastChannel);
}

/**
 * Web Locks com exclusão real e ordem FIFO, partilhada por todos os "separadores" (módulos
 * isolados) do teste. Sem isto o jsdom cai no caminho sem lock e a serialização entre
 * separadores nunca seria exercida.
 */
export function installWebLocks(): { request: ReturnType<typeof vi.fn> } {
  let tail: Promise<unknown> = Promise.resolve();

  const request = vi.fn((_name: string, callback: () => Promise<unknown>) => {
    const run = tail.then(() => callback());
    tail = run.catch(() => undefined);
    return run;
  });

  Object.defineProperty(navigator, 'locks', { value: { request }, configurable: true });
  return { request };
}

export function uninstallWebLocks(): void {
  Reflect.deleteProperty(navigator, 'locks');
}

/**
 * `matchMedia` que o jsdom não implementa, a responder a `(min-width: Npx)` contra a largura
 * indicada. Qualquer outra media query (ex.: tema escuro do sistema) responde `false`.
 */
export function setViewport(width: number): void {
  const matches = (query: string): boolean => {
    const minWidth = /\(min-width:\s*(\d+)px\)/.exec(query)?.[1];
    return minWidth !== undefined && width >= Number(minWidth);
  };

  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: (query: string): MediaQueryList =>
      ({
        matches: matches(query),
        media: query,
        onchange: null,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        addListener: () => undefined,
        removeListener: () => undefined,
        dispatchEvent: () => false,
      }) as MediaQueryList,
  });
}
