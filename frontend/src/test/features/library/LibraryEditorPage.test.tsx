import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { resetPendingExerciseVideo } from '@/shared/lib/pending-video';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  exercise,
  EXERCISE_ID,
  food,
  FOOD_ID,
  GLOBAL_EXERCISE_ID,
  libraryPage,
  supplement,
  SUPPLEMENT_ID,
} from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';
import { stubStorage, stubVideoMetadata } from '@/test/video-fakes';

const BASE = '/trainer/library';
const emptyLists = () => [
  http.get(`${API}/exercises`, () => HttpResponse.json(libraryPage([]))),
  http.get(`${API}/foods`, () => HttpResponse.json(libraryPage([]))),
  http.get(`${API}/supplements`, () => HttpResponse.json(libraryPage([]))),
];

beforeEach(() => resetPendingExerciseVideo());

describe('LibraryEditorPage', () => {
  it('edits a food in a page and returns to the foods tab', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...restorableSession(),
      ...emptyLists(),
      http.get(`${API}/foods/${FOOD_ID}`, () => HttpResponse.json(food())),
      http.patch(`${API}/foods/${FOOD_ID}`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(food({ name: 'Batido de whey' }));
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${BASE}/foods/${FOOD_ID}`] });

    expect(await screen.findByRole('heading', { name: 'Editar Batido pós-treino' })).toBeVisible();
    const name = screen.getByLabelText('Nome');
    await user.clear(name);
    await user.type(name, 'Batido de whey');
    await user.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() =>
      expect(router.state.location.pathname + router.state.location.search).toBe(
        `${BASE}?tab=foods`
      )
    );
    expect(body).toMatchObject({ name: 'Batido de whey', protein: 31 });
    expect(screen.queryByRole('alertdialog')).toBeNull();
  }, 20000);

  it('edits a supplement in a page and returns to the supplements tab', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...restorableSession(),
      ...emptyLists(),
      http.get(`${API}/supplements/${SUPPLEMENT_ID}`, () => HttpResponse.json(supplement())),
      http.patch(`${API}/supplements/${SUPPLEMENT_ID}`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(supplement({ serving_size: '2' }));
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${BASE}/supplements/${SUPPLEMENT_ID}`] });

    const dose = await screen.findByLabelText('Dose');
    await user.clear(dose);
    await user.type(dose, '2');
    await user.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(router.state.location.search).toBe('?tab=supplements'));
    expect(body).toMatchObject({ serving_size: '2', unit_of_measure: 'cápsula' });
  }, 20000);

  it('opens a global item read-only, with no form', async () => {
    server.use(
      ...restorableSession(),
      ...emptyLists(),
      http.get(`${API}/exercises/${GLOBAL_EXERCISE_ID}`, () =>
        HttpResponse.json(
          exercise({ id: GLOBAL_EXERCISE_ID, scope: 'global', name: 'Agachamento' })
        )
      )
    );
    renderApp({ initialEntries: [`${BASE}/exercises/${GLOBAL_EXERCISE_ID}`] });

    expect(await screen.findByText('Só de leitura.')).toBeVisible();
    expect(screen.getByText(/Item global da plataforma/)).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Guardar' })).toBeNull();
    expect(screen.queryByLabelText('Nome')).toBeNull();
    expect(screen.getByRole('link', { name: 'Voltar a Exercícios' })).toHaveAttribute(
      'href',
      '/trainer/library'
    );
  }, 15000);

  it('shows a not-found state when the item does not exist', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/foods/${FOOD_ID}`, () =>
        HttpResponse.json(problem('food_not_found', { status: 404 }), { status: 404 })
      )
    );
    renderApp({ initialEntries: [`${BASE}/foods/${FOOD_ID}`] });

    expect(await screen.findByText('Item não encontrado')).toBeVisible();
    expect(screen.getByRole('link', { name: 'Voltar a Alimentos' })).toHaveAttribute(
      'href',
      '/trainer/library?tab=foods'
    );
  }, 15000);

  it('asks before leaving with unsaved changes and keeps the text on «Continuar a editar»', async () => {
    server.use(...restorableSession(), ...emptyLists());
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${BASE}/foods/new`] });

    await user.type(await screen.findByLabelText('Nome'), 'Arroz');
    await user.click(screen.getByRole('link', { name: 'Voltar a Alimentos' }));

    const dialog = await screen.findByRole('alertdialog', { name: 'Sair sem guardar?' });
    expect(dialog).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Continuar a editar' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).toBeNull());
    expect(screen.getByLabelText('Nome')).toHaveValue('Arroz');
    expect(router.state.location.pathname).toBe(`${BASE}/foods/new`);

    await user.click(screen.getByRole('link', { name: 'Voltar a Alimentos' }));
    await user.click(await screen.findByRole('button', { name: 'Sair sem guardar' }));
    await waitFor(() => expect(router.state.location.pathname).toBe(BASE));
  }, 20000);

  it('does not ask when nothing changed', async () => {
    server.use(...restorableSession(), ...emptyLists());
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${BASE}/supplements/new`] });

    await screen.findByLabelText('Nome');
    await user.click(screen.getByRole('link', { name: 'Voltar a Suplementos' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(BASE));
    expect(screen.queryByRole('alertdialog')).toBeNull();
  }, 15000);

  describe('video on create', () => {
    const video = (status: string) => ({
      id: '99999999-9999-9999-9999-999999999998',
      exercise_id: EXERCISE_ID,
      scope: 'private',
      status,
      content_type: 'video/mp4',
      declared_size_bytes: 11,
      size_bytes: null,
      duration_milliseconds: null,
      width: null,
      height: null,
      video_codec: null,
      audio_codec: null,
      failure_code: null,
      upload_expires_at: '2026-09-29T10:15:00Z',
      ready_at: null,
      created_at: '2026-09-29T10:00:00Z',
      updated_at: '2026-09-29T10:00:00Z',
    });
    const mp4 = () => new File(['video-bytes'], 'remada.mp4', { type: 'video/mp4' });

    it('creates the exercise and starts the upload on its edit page', async () => {
      const calls: string[] = [];
      const storage = stubStorage();
      stubVideoMetadata();
      server.use(
        ...restorableSession(),
        ...emptyLists(),
        http.post(`${API}/exercises`, () => {
          calls.push('create');
          return HttpResponse.json(exercise(), { status: 201 });
        }),
        http.get(`${API}/exercises/${EXERCISE_ID}`, () => HttpResponse.json(exercise())),
        http.post(`${API}/exercises/${EXERCISE_ID}/video/uploads`, () => {
          calls.push('upload request');
          return HttpResponse.json(
            {
              video: video('pending'),
              upload: {
                method: 'PUT',
                url: 'https://storage.test/upload/remada',
                content_type: 'video/mp4',
                expires_at: '2026-09-29T10:15:00Z',
              },
              max_size_bytes: 104857600,
            },
            { status: 201 }
          );
        }),
        http.post(`${API}/exercises/${EXERCISE_ID}/video/uploads/:videoId/complete`, () => {
          calls.push('complete');
          return HttpResponse.json(video('processing'));
        }),
        http.get(`${API}/exercises/${EXERCISE_ID}/video/uploads/:videoId`, () =>
          HttpResponse.json(video('ready'))
        )
      );
      const user = userEvent.setup({ applyAccept: false });
      const { router } = renderApp({ initialEntries: [`${BASE}/exercises/new`] });

      await user.type(await screen.findByLabelText('Nome'), 'Remada curvada');
      await user.upload(screen.getByLabelText('Vídeo (opcional)'), mp4());
      await user.click(screen.getByRole('button', { name: 'Criar' }));

      expect(await screen.findByText('Vídeo pronto.')).toBeInTheDocument();
      expect(router.state.location.pathname).toBe(`${BASE}/exercises/${EXERCISE_ID}`);
      expect(calls).toEqual(['create', 'upload request', 'complete']);
      expect(storage).toHaveLength(1);
      expect(screen.getByRole('heading', { name: 'Editar Remada curvada' })).toBeVisible();
    }, 20000);

    it('refuses a file of the wrong type before creating anything', async () => {
      let creates = 0;
      server.use(
        ...restorableSession(),
        ...emptyLists(),
        http.post(`${API}/exercises`, () => {
          creates += 1;
          return HttpResponse.json(exercise(), { status: 201 });
        })
      );
      const user = userEvent.setup({ applyAccept: false });
      const { router } = renderApp({ initialEntries: [`${BASE}/exercises/new`] });

      await user.type(await screen.findByLabelText('Nome'), 'Remada curvada');
      await user.upload(
        screen.getByLabelText('Vídeo (opcional)'),
        new File(['x'], 'notas.txt', { type: 'text/plain' })
      );
      await user.click(screen.getByRole('button', { name: 'Criar' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Formato não suportado. Usa um vídeo MP4 ou MOV.'
      );
      expect(creates).toBe(0);
      expect(router.state.location.pathname).toBe(`${BASE}/exercises/new`);
    }, 15000);

    it('asks before leaving when only a file was chosen', async () => {
      server.use(...restorableSession(), ...emptyLists());
      const user = userEvent.setup({ applyAccept: false });
      renderApp({ initialEntries: [`${BASE}/exercises/new`] });

      await user.upload(await screen.findByLabelText('Vídeo (opcional)'), mp4());
      await user.click(screen.getByRole('link', { name: 'Voltar a Exercícios' }));

      expect(await screen.findByRole('alertdialog', { name: 'Sair sem guardar?' })).toBeVisible();
    }, 15000);
  });
});
