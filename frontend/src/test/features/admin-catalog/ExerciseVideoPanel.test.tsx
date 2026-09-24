import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const EXERCISE_ID = '22222222-2222-2222-2222-222222222222';
const VIDEO_ID = '44444444-4444-4444-4444-444444444444';
const STORAGE_URL = 'https://storage.test/pt-manager/videos/upload-1';

const exercise = {
  id: EXERCISE_ID,
  name: 'Agachamento',
  description: null,
  muscle_groups: null,
  equipment: null,
  difficulty_level: null,
  video_url: null,
  managed_video_status: null as string | null,
  has_ready_video: false,
  is_active: true,
  created_at: '2026-09-23T10:00:00Z',
  updated_at: '2026-09-23T10:00:00Z',
};

function video(status: string, failureCode: string | null = null) {
  return {
    id: VIDEO_ID,
    exercise_id: EXERCISE_ID,
    scope: 'global',
    status,
    content_type: 'video/mp4',
    declared_size_bytes: 11,
    size_bytes: null,
    duration_milliseconds: null,
    width: null,
    height: null,
    video_codec: null,
    audio_codec: null,
    failure_code: failureCode,
    upload_expires_at: '2026-09-23T10:15:00Z',
    ready_at: null,
    created_at: '2026-09-23T10:00:00Z',
    updated_at: '2026-09-23T10:00:00Z',
  };
}

const uploadResponse = {
  video: video('pending'),
  upload: {
    method: 'PUT',
    url: STORAGE_URL,
    content_type: 'video/mp4',
    expires_at: '2026-09-23T10:15:00Z',
  },
  max_size_bytes: 104857600,
};

const mp4 = () => new File(['video-bytes'], 'agachamento.mp4', { type: 'video/mp4' });

interface StorageRequest {
  method: string;
  url: string;
  headers: Record<string, string>;
  body: unknown;
}

/**
 * Storage de terceiros (R2) visto pelo browser: um XHR falso que regista o pedido, emite
 * progresso e responde com `status`. A API continua no MSW; só o PUT assinado sai daqui.
 */
function stubStorage(status = 200): StorageRequest[] {
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
function stubVideoMetadata({ duration = 30, width = 1280, height = 720 } = {}) {
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

/** Abre o exercício no catálogo com o estado de vídeo pedido. */
async function openExercise(overrides: Partial<typeof exercise> = {}) {
  server.use(
    ...restorableSession({ role: 'superuser', trainer_id: null }),
    http.get(`${API}/global-exercises`, () =>
      HttpResponse.json({
        items: [{ ...exercise, ...overrides }],
        total_count: 1,
        page_number: 1,
        page_size: 25,
      })
    )
  );
  stubVideoMetadata();
  const user = userEvent.setup({ applyAccept: false });
  renderApp({ initialEntries: ['/admin/catalog/exercises'] });
  await user.click(await screen.findByRole('button', { name: 'Editar Agachamento' }));
  return user;
}

describe('ExerciseVideoPanel', () => {
  it('uploads straight to storage with the signed content type, then tracks it to ready', async () => {
    const calls: string[] = [];
    const storage = stubStorage();
    server.use(
      http.post(`${API}/global-exercises/:exerciseId/video/uploads`, async ({ request }) => {
        calls.push('request');
        expect(await request.json()).toEqual({ content_type: 'video/mp4', size_bytes: 11 });
        return HttpResponse.json(uploadResponse, { status: 201 });
      }),
      http.post(`${API}/global-exercises/:exerciseId/video/uploads/:videoId/complete`, () => {
        calls.push(`complete after ${storage.length} storage upload`);
        return HttpResponse.json(video('processing'));
      }),
      http.get(`${API}/global-exercises/:exerciseId/video/uploads/:videoId`, () => {
        calls.push('status');
        return HttpResponse.json(video('ready'));
      })
    );
    const user = await openExercise();
    const file = mp4();

    await user.upload(screen.getByLabelText('Enviar vídeo'), file);

    expect(await screen.findByText('Vídeo pronto.')).toBeInTheDocument();
    expect(calls).toEqual(['request', 'complete after 1 storage upload', 'status']);
    // Content-Type exatamente o assinado; o token de acesso nunca segue para o storage.
    expect(storage).toEqual([
      { method: 'PUT', url: STORAGE_URL, headers: { 'content-type': 'video/mp4' }, body: file },
    ]);
    expect(screen.getByRole('button', { name: 'Ver vídeo' })).toBeVisible();
  }, 15000);

  it('rejects an unsupported file before any request', async () => {
    let requests = 0;
    server.use(
      http.post(`${API}/global-exercises/:exerciseId/video/uploads`, () => {
        requests += 1;
        return HttpResponse.json(uploadResponse, { status: 201 });
      })
    );
    const user = await openExercise();

    await user.upload(
      screen.getByLabelText('Enviar vídeo'),
      new File(['x'], 'notas.txt', { type: 'text/plain' })
    );

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Formato não suportado. Usa um vídeo MP4 ou MOV.'
    );
    expect(requests).toBe(0);
  }, 15000);

  it('refuses a video longer than three minutes before any request', async () => {
    let requests = 0;
    server.use(
      http.post(`${API}/global-exercises/:exerciseId/video/uploads`, () => {
        requests += 1;
        return HttpResponse.json(uploadResponse, { status: 201 });
      })
    );
    const user = await openExercise();
    stubVideoMetadata({ duration: 240 });

    await user.upload(screen.getByLabelText('Enviar vídeo'), mp4());

    expect(await screen.findByRole('alert')).toHaveTextContent('O vídeo tem de ter até 3 minutos.');
    expect(requests).toBe(0);
  }, 15000);

  it('explains a rejected video with the backend failure code', async () => {
    stubStorage();
    server.use(
      http.post(`${API}/global-exercises/:exerciseId/video/uploads`, () =>
        HttpResponse.json(uploadResponse, { status: 201 })
      ),
      http.post(`${API}/global-exercises/:exerciseId/video/uploads/:videoId/complete`, () =>
        HttpResponse.json(video('processing'))
      ),
      http.get(`${API}/global-exercises/:exerciseId/video/uploads/:videoId`, () =>
        HttpResponse.json(video('rejected', 'exercise_video_codec_unsupported'))
      )
    );
    const user = await openExercise();

    await user.upload(screen.getByLabelText('Enviar vídeo'), mp4());

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Codec de vídeo não suportado. Usa H.264.'
    );
  }, 15000);

  it('does not confirm the upload when storage refuses the file', async () => {
    stubStorage(403);
    let completes = 0;
    server.use(
      http.post(`${API}/global-exercises/:exerciseId/video/uploads`, () =>
        HttpResponse.json(uploadResponse, { status: 201 })
      ),
      http.post(`${API}/global-exercises/:exerciseId/video/uploads/:videoId/complete`, () => {
        completes += 1;
        return HttpResponse.json(video('processing'));
      })
    );
    const user = await openExercise();

    await user.upload(screen.getByLabelText('Enviar vídeo'), mp4());

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'O envio para o armazenamento falhou. Tenta novamente.'
    );
    expect(completes).toBe(0);
  }, 15000);

  it('shows the provider outage in Portuguese', async () => {
    server.use(
      http.post(`${API}/global-exercises/:exerciseId/video/uploads`, () =>
        HttpResponse.json(problem('exercise_video_storage_unavailable', { status: 503 }), {
          status: 503,
        })
      )
    );
    const user = await openExercise();

    await user.upload(screen.getByLabelText('Enviar vídeo'), mp4());

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'O serviço de vídeo não está disponível de momento.'
    );
  }, 15000);

  it('plays the ready video from a signed URL', async () => {
    server.use(
      http.get(`${API}/global-exercises/:exerciseId/video`, () =>
        HttpResponse.json({
          video_id: VIDEO_ID,
          exercise_id: EXERCISE_ID,
          content_type: 'video/mp4',
          duration_milliseconds: 30000,
          width: 1280,
          height: 720,
          playback_url: 'https://storage.test/signed-playback',
          expires_at: '2026-09-23T10:30:00Z',
        })
      )
    );
    const user = await openExercise({ managed_video_status: 'ready', has_ready_video: true });

    await user.click(screen.getByRole('button', { name: 'Ver vídeo' }));

    expect(await screen.findByLabelText('Vídeo de Agachamento')).toHaveAttribute(
      'src',
      'https://storage.test/signed-playback'
    );
  }, 15000);

  it('removes the video only after confirmation', async () => {
    let deletes = 0;
    server.use(
      http.delete(`${API}/global-exercises/:exerciseId/video`, () => {
        deletes += 1;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = await openExercise({ managed_video_status: 'ready', has_ready_video: true });

    await user.click(screen.getByRole('button', { name: 'Remover vídeo' }));
    expect(deletes).toBe(0);
    await user.click(screen.getByRole('button', { name: 'Confirmar remoção' }));

    await waitFor(() => expect(deletes).toBe(1));
    expect(await screen.findByLabelText('Enviar vídeo')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remover vídeo' })).not.toBeInTheDocument();
  }, 15000);
});
