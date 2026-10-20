import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { resetPendingExerciseVideo } from '@/features/admin-catalog/lib/pending-video';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';
import { stubStorage, stubVideoMetadata } from '@/test/video-fakes';

const EXERCISE_ID = '22222222-2222-2222-2222-222222222222';
const SUPPLEMENT_ID = '33333333-3333-3333-3333-333333333333';
const VIDEO_ID = '44444444-4444-4444-4444-444444444444';
const STAMPS = { created_at: '2026-09-23T10:00:00Z', updated_at: '2026-09-23T10:00:00Z' };

const food = {
  id: '11111111-1111-1111-1111-111111111111',
  name: 'Arroz',
  description: null,
  protein: 3,
  carbs: 28,
  fats: 1,
  kcal: 133,
  fiber: null,
  default_serving_grams: 150,
  is_active: true,
  ...STAMPS,
};
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
  ...STAMPS,
};
const supplement = {
  id: SUPPLEMENT_ID,
  name: 'Creatina',
  description: null,
  unit_of_measure: 'g',
  serving_size: '5',
  timing: 'Manhã' as string | null,
  trainer_notes: null,
  is_active: true,
  ...STAMPS,
};

const session = () => restorableSession({ role: 'superuser', trainer_id: null });
const emptyList = (path: string) =>
  http.get(`${API}/${path}`, () =>
    HttpResponse.json({ items: [], total_count: 0, page_number: 1, page_size: 25 })
  );

beforeEach(() => resetPendingExerciseVideo());

describe('CatalogEditorPage', () => {
  it('follows the «Novo exercício» link to the create page', async () => {
    server.use(...session(), emptyList('global-exercises'));
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/admin/catalog/exercises'] });

    await user.click(await screen.findByRole('link', { name: /Novo exercício/ }));

    expect(await screen.findByRole('heading', { name: 'Criar exercício' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/catalog/exercises/new');
    expect(screen.getByText('Os dados ficam disponíveis no catálogo global.')).toBeVisible();
    expect(screen.getByRole('link', { name: 'Voltar a Exercícios' })).toHaveAttribute(
      'href',
      '/admin/catalog/exercises'
    );
    expect(screen.getByPlaceholderText('ex.: Barra')).toBeVisible();
  }, 15000);

  it('creates a food through the page and returns to the list', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...session(),
      emptyList('global-foods'),
      http.post(`${API}/global-foods`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(food, { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/admin/catalog/foods/new'] });

    await user.type(await screen.findByLabelText('Nome'), 'Arroz');
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/catalog/foods'));
    expect(body).toMatchObject({ name: 'Arroz', description: null });
    expect(
      await screen.findByRole('heading', { name: /Catálogo global de alimentos/ })
    ).toBeVisible();
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  }, 15000);

  it('creates a supplement without timing as null', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...session(),
      emptyList('global-supplements'),
      http.post(`${API}/global-supplements`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...supplement, timing: null }, { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/admin/catalog/supplements/new'] });

    await user.type(await screen.findByLabelText('Nome'), 'Creatina');
    await user.type(screen.getByLabelText('Unidade'), 'g');
    await user.type(screen.getByLabelText('Dose'), '5');
    expect(screen.getByLabelText('Momento (opcional)')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/catalog/supplements'));
    expect(body).toMatchObject({ name: 'Creatina', timing: null });
  }, 15000);

  it('edits a supplement and sends the cleared timing as null', async () => {
    let body: Record<string, unknown> | null = null;
    server.use(
      ...session(),
      emptyList('global-supplements'),
      http.get(`${API}/global-supplements/:supplementId`, () => HttpResponse.json(supplement)),
      http.patch(`${API}/global-supplements/:supplementId`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(supplement);
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({
      initialEntries: [`/admin/catalog/supplements/${SUPPLEMENT_ID}`],
    });

    expect(await screen.findByRole('heading', { name: 'Editar Creatina' })).toBeVisible();
    const timing = screen.getByLabelText('Momento (opcional)');
    expect(timing).toHaveValue('Manhã');
    await user.clear(timing);
    await user.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/catalog/supplements'));
    expect(body).toMatchObject({ timing: null, serving_size: '5' });
  }, 15000);

  it('shows a not-found state with the way back when the item does not exist', async () => {
    server.use(
      ...session(),
      http.get(`${API}/global-foods/:foodId`, () =>
        HttpResponse.json(problem('global_food_not_found', { status: 404 }), { status: 404 })
      )
    );
    renderApp({ initialEntries: [`/admin/catalog/foods/${food.id}`] });

    expect(await screen.findByText('Item não encontrado')).toBeVisible();
    expect(screen.getByRole('link', { name: 'Voltar a Alimentos' })).toBeVisible();
  }, 15000);

  describe('exercise with a video', () => {
    const uploadResponse = {
      video: {
        id: VIDEO_ID,
        exercise_id: EXERCISE_ID,
        scope: 'global',
        status: 'pending',
        content_type: 'video/mp4',
        declared_size_bytes: 11,
        size_bytes: null,
        duration_milliseconds: null,
        width: null,
        height: null,
        video_codec: null,
        audio_codec: null,
        failure_code: null,
        upload_expires_at: '2026-09-23T10:15:00Z',
        ready_at: null,
        ...STAMPS,
      },
      upload: {
        method: 'PUT',
        url: 'https://storage.test/pt-manager/videos/upload-1',
        content_type: 'video/mp4',
        expires_at: '2026-09-23T10:15:00Z',
      },
      max_size_bytes: 104857600,
    };
    const mp4 = () => new File(['video-bytes'], 'agachamento.mp4', { type: 'video/mp4' });

    it('creates the exercise and starts the upload on its edit page', async () => {
      const calls: string[] = [];
      const storage = stubStorage();
      stubVideoMetadata();
      server.use(
        ...session(),
        http.post(`${API}/global-exercises`, () => {
          calls.push('create');
          return HttpResponse.json(exercise, { status: 201 });
        }),
        http.get(`${API}/global-exercises/:exerciseId`, () => HttpResponse.json(exercise)),
        http.post(`${API}/global-exercises/:exerciseId/video/uploads`, () => {
          calls.push('upload request');
          return HttpResponse.json(uploadResponse, { status: 201 });
        }),
        http.post(`${API}/global-exercises/:exerciseId/video/uploads/:videoId/complete`, () => {
          calls.push('complete');
          return HttpResponse.json({ ...uploadResponse.video, status: 'processing' });
        }),
        http.get(`${API}/global-exercises/:exerciseId/video/uploads/:videoId`, () =>
          HttpResponse.json({ ...uploadResponse.video, status: 'ready' })
        )
      );
      const user = userEvent.setup({ applyAccept: false });
      const { router } = renderApp({ initialEntries: ['/admin/catalog/exercises/new'] });

      await user.type(await screen.findByLabelText('Nome'), 'Agachamento');
      await user.upload(screen.getByLabelText('Vídeo (opcional)'), mp4());
      await user.click(screen.getByRole('button', { name: 'Criar' }));

      expect(await screen.findByText('Vídeo pronto.')).toBeInTheDocument();
      expect(router.state.location.pathname).toBe(`/admin/catalog/exercises/${EXERCISE_ID}`);
      expect(calls).toEqual(['create', 'upload request', 'complete']);
      expect(storage).toHaveLength(1);
      expect(screen.getByRole('heading', { name: 'Editar Agachamento' })).toBeVisible();
    }, 15000);

    it('keeps the exercise created when the upload request fails', async () => {
      let creates = 0;
      stubVideoMetadata();
      server.use(
        ...session(),
        http.post(`${API}/global-exercises`, () => {
          creates += 1;
          return HttpResponse.json(exercise, { status: 201 });
        }),
        http.get(`${API}/global-exercises/:exerciseId`, () => HttpResponse.json(exercise)),
        http.post(`${API}/global-exercises/:exerciseId/video/uploads`, () =>
          HttpResponse.json(problem('exercise_video_storage_unavailable', { status: 503 }), {
            status: 503,
          })
        )
      );
      const user = userEvent.setup({ applyAccept: false });
      const { router } = renderApp({ initialEntries: ['/admin/catalog/exercises/new'] });

      await user.type(await screen.findByLabelText('Nome'), 'Agachamento');
      await user.upload(screen.getByLabelText('Vídeo (opcional)'), mp4());
      await user.click(screen.getByRole('button', { name: 'Criar' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'O serviço de vídeo não está disponível de momento.'
      );
      expect(router.state.location.pathname).toBe(`/admin/catalog/exercises/${EXERCISE_ID}`);
      expect(creates).toBe(1);
    }, 15000);

    it('warns before leaving while the video is being sent, and not after it is done', async () => {
      let release: () => void = () => undefined;
      const gate = new Promise<void>((resolve) => {
        release = resolve;
      });
      stubStorage();
      stubVideoMetadata();
      server.use(
        ...session(),
        emptyList('global-exercises'),
        http.post(`${API}/global-exercises`, () => HttpResponse.json(exercise, { status: 201 })),
        http.get(`${API}/global-exercises/:exerciseId`, () => HttpResponse.json(exercise)),
        http.post(`${API}/global-exercises/:exerciseId/video/uploads`, () =>
          HttpResponse.json(uploadResponse, { status: 201 })
        ),
        // O envio fica "a confirmar" até libertarmos a promessa: o painel está a enviar.
        http.post(
          `${API}/global-exercises/:exerciseId/video/uploads/:videoId/complete`,
          async () => {
            await gate;
            return HttpResponse.json({ ...uploadResponse.video, status: 'processing' });
          }
        ),
        http.get(`${API}/global-exercises/:exerciseId/video/uploads/:videoId`, () =>
          HttpResponse.json({ ...uploadResponse.video, status: 'ready' })
        )
      );
      const user = userEvent.setup({ applyAccept: false });
      const { router } = renderApp({ initialEntries: ['/admin/catalog/exercises/new'] });

      await user.type(await screen.findByLabelText('Nome'), 'Agachamento');
      await user.upload(screen.getByLabelText('Vídeo (opcional)'), mp4());
      await user.click(screen.getByRole('button', { name: 'Criar' }));
      await screen.findByText('A confirmar o envio…');

      await user.click(screen.getByRole('link', { name: 'Voltar a Exercícios' }));
      const dialog = await screen.findByRole('alertdialog', {
        name: 'Sair durante o envio do vídeo?',
      });
      expect(dialog).toHaveTextContent(
        'O vídeo ainda está a ser enviado. Se saíres, o envio é cancelado.'
      );
      await user.click(screen.getByRole('button', { name: 'Continuar a editar' }));
      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(router.state.location.pathname).toBe(`/admin/catalog/exercises/${EXERCISE_ID}`);

      release();
      expect(await screen.findByText('Vídeo pronto.')).toBeInTheDocument();
      await user.click(screen.getByRole('link', { name: 'Voltar a Exercícios' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/admin/catalog/exercises'));
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    }, 20000);

    it('refuses a file of the wrong type before any request', async () => {
      let creates = 0;
      server.use(
        ...session(),
        http.post(`${API}/global-exercises`, () => {
          creates += 1;
          return HttpResponse.json(exercise, { status: 201 });
        })
      );
      const user = userEvent.setup({ applyAccept: false });
      const { router } = renderApp({ initialEntries: ['/admin/catalog/exercises/new'] });

      await user.type(await screen.findByLabelText('Nome'), 'Agachamento');
      await user.upload(
        screen.getByLabelText('Vídeo (opcional)'),
        new File(['x'], 'notas.txt', { type: 'text/plain' })
      );
      await user.click(screen.getByRole('button', { name: 'Criar' }));

      expect(await screen.findByRole('alert')).toHaveTextContent(
        'Formato não suportado. Usa um vídeo MP4 ou MOV.'
      );
      expect(creates).toBe(0);
      expect(router.state.location.pathname).toBe('/admin/catalog/exercises/new');
    }, 15000);
  });

  describe('unsaved changes guard', () => {
    it('asks before leaving, keeps the text on «Continuar a editar» and leaves on «Sair sem guardar»', async () => {
      server.use(...session(), emptyList('global-foods'));
      const user = userEvent.setup();
      const { router } = renderApp({ initialEntries: ['/admin/catalog/foods/new'] });

      await user.type(await screen.findByLabelText('Nome'), 'Arroz');
      await user.click(screen.getByRole('link', { name: 'Alimentos' }));

      const dialog = await screen.findByRole('alertdialog', { name: 'Sair sem guardar?' });
      expect(dialog).toHaveTextContent('As alterações a este item perdem-se.');
      await user.click(screen.getByRole('button', { name: 'Continuar a editar' }));
      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(screen.getByLabelText('Nome')).toHaveValue('Arroz');
      expect(router.state.location.pathname).toBe('/admin/catalog/foods/new');

      await user.click(screen.getByRole('link', { name: 'Alimentos' }));
      await user.click(await screen.findByRole('button', { name: 'Sair sem guardar' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/admin/catalog/foods'));
    }, 15000);

    it('does not ask when nothing changed', async () => {
      server.use(...session(), emptyList('global-foods'));
      const user = userEvent.setup();
      const { router } = renderApp({ initialEntries: ['/admin/catalog/foods/new'] });

      await screen.findByLabelText('Nome');
      await user.click(screen.getByRole('link', { name: 'Voltar a Alimentos' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/admin/catalog/foods'));
      expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    }, 15000);
  });
});
