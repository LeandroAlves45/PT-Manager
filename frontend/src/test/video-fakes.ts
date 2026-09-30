import { vi } from 'vitest';

/**
 * Fakes do browser para testes de vídeo (admin e biblioteca do trainer).
 *
 * O upload vai direto ao storage de terceiros por XHR (fora da API, por isso fora do MSW), e o
 * `<video>` do jsdom não lê metadados. `setup.ts` repõe os stubs e spies no fim de cada teste.
 */

export interface StorageRequest {
  method: string;
  url: string;
  headers: Record<string, string>;
  body: unknown;
}

/**
 * Storage de terceiros (R2) visto pelo browser: um XHR falso que regista o pedido, emite
 * progresso e responde com `status`. A API continua no MSW; só o PUT assinado sai daqui.
 */
export function stubStorage(status = 200): StorageRequest[] {
  const requests: StorageRequest[] = [];
  class FakeStorageXhr {
    upload: { onprogress: ((event: ProgressEvent) => void) | null } = { onprogress: null };
    onload: (() => void) | null = null;
    onerror: (() => void) | null = null;
    onabort: (() => void) | null = null;
    status = 0;
    private request: StorageRequest = { method: '', url: '', headers: {}, body: null };

    open(method: string, url: string) {
      this.request = { method, url, headers: {}, body: null };
    }
    setRequestHeader(name: string, value: string) {
      this.request.headers[name.toLowerCase()] = value;
    }
    send(body: unknown) {
      requests.push({ ...this.request, body });
      queueMicrotask(() => {
        this.upload.onprogress?.(
          new ProgressEvent('progress', { lengthComputable: true, loaded: 11, total: 11 })
        );
        this.status = status;
        this.onload?.();
      });
    }
    abort() {
      this.onabort?.();
    }
  }
  vi.stubGlobal('XMLHttpRequest', FakeStorageXhr);
  return requests;
}

/**
 * O `<video>` do jsdom não carrega media: simula o browser a ler os metadados do ficheiro.
 * `setup.ts` repõe os spies no fim de cada teste.
 */
export function stubVideoMetadata({ duration = 30, width = 1280, height = 720 } = {}) {
  vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:video');
  vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
  vi.spyOn(HTMLMediaElement.prototype, 'duration', 'get').mockReturnValue(duration);
  vi.spyOn(HTMLVideoElement.prototype, 'videoWidth', 'get').mockReturnValue(width);
  vi.spyOn(HTMLVideoElement.prototype, 'videoHeight', 'get').mockReturnValue(height);
  vi.spyOn(HTMLMediaElement.prototype, 'src', 'set').mockImplementation(function (
    this: HTMLMediaElement
  ) {
    queueMicrotask(() => this.dispatchEvent(new Event('loadedmetadata')));
  });
}
