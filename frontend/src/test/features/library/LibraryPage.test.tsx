import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

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

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const ROUTE = '/trainer/library';

const globalExercise = () =>
  exercise({
    id: GLOBAL_EXERCISE_ID,
    scope: 'global',
    name: 'Agachamento com barra',
    muscle_groups: 'quadriceps,glutes',
    has_ready_video: true,
    managed_video_status: 'ready',
  });

type Page<T> = ReturnType<typeof libraryPage<T>>;

/** Sessão de trainer e as três listas; devolve os URLs pedidos por separador. */
function libraryHandlers({
  exercises = libraryPage([globalExercise(), exercise()]),
  foods = libraryPage([food()]),
  supplements = libraryPage([supplement()]),
}: {
  exercises?: Page<ReturnType<typeof exercise>>;
  foods?: Page<ReturnType<typeof food>>;
  supplements?: Page<ReturnType<typeof supplement>>;
} = {}) {
  const urls = { exercises: [] as URL[], foods: [] as URL[], supplements: [] as URL[] };
  server.use(
    ...restorableSession(),
    http.get(`${API}/exercises`, ({ request }) => {
      urls.exercises.push(new URL(request.url));
      return HttpResponse.json(exercises);
    }),
    http.get(`${API}/foods`, ({ request }) => {
      urls.foods.push(new URL(request.url));
      return HttpResponse.json(foods);
    }),
    http.get(`${API}/supplements`, ({ request }) => {
      urls.supplements.push(new URL(request.url));
      return HttpResponse.json(supplements);
    })
  );
  return urls;
}

const rowOf = async (name: string) =>
  (await screen.findByRole('rowheader', { name: new RegExp(`^${name}`) })).closest('tr')!;

describe('Trainer library', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('shows global items read-only and private items with edit and archive', async () => {
    libraryHandlers();
    renderApp({ initialEntries: [ROUTE] });

    const globalRow = await rowOf('Agachamento com barra');
    expect(within(globalRow).getByText('Global')).toBeInTheDocument();
    expect(within(globalRow).getByText('Quadríceps, Glúteos')).toBeInTheDocument();
    expect(within(globalRow).getByText('Com vídeo')).toBeInTheDocument();
    expect(
      within(globalRow).getByRole('button', { name: 'Ver Agachamento com barra' })
    ).toBeVisible();
    expect(within(globalRow).queryByRole('button', { name: /^Editar/ })).toBeNull();
    expect(within(globalRow).queryByRole('button', { name: /^Arquivar/ })).toBeNull();

    const privateRow = await rowOf('Remada curvada');
    expect(within(privateRow).getByText('Privado')).toBeInTheDocument();
    expect(within(privateRow).getByText('Costas, Bíceps')).toBeInTheDocument();
    expect(within(privateRow).getByRole('button', { name: 'Editar Remada curvada' })).toBeVisible();
    expect(
      within(privateRow).getByRole('button', { name: 'Arquivar Remada curvada' })
    ).toBeVisible();
  }, 15000);

  it('reads tab, search, state and page from the URL and resets them on tab change', async () => {
    const urls = libraryHandlers({ foods: libraryPage([food()], 30, 2) });
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=foods&search=batido&activity=all&page=2`] });

    await rowOf('Batido pós-treino');
    const first = urls.foods[0]!;
    expect(Object.fromEntries(first.searchParams)).toEqual({
      search: 'batido',
      activity: 'all',
      page_number: '2',
      page_size: '25',
    });
    expect(urls.exercises).toHaveLength(0);

    await user.click(screen.getByRole('tab', { name: 'Suplementos' }));

    await rowOf('Multivitamínico');
    expect(window.location.search).toBe('?tab=supplements');
    expect(Object.fromEntries(urls.supplements[0]!.searchParams)).toEqual({
      activity: 'active',
      page_number: '1',
      page_size: '25',
    });
  }, 15000);

  it('searches on the server after the debounce and goes back to the first page', async () => {
    const urls = libraryHandlers({ exercises: libraryPage([exercise()], 30, 2) });
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?page=2`] });
    await rowOf('Remada curvada');

    await user.type(screen.getByRole('searchbox', { name: 'Pesquisar exercícios' }), 'rem');

    await waitFor(() =>
      expect(urls.exercises.some((url) => url.searchParams.get('search') === 'rem')).toBe(true)
    );
    const searched = urls.exercises.find((url) => url.searchParams.get('search') === 'rem')!;
    expect(searched.searchParams.get('page_number')).toBe('1');
    expect(urls.exercises.filter((url) => url.searchParams.has('search'))).toHaveLength(1);
    expect(window.location.search).toBe('?search=rem');
  }, 15000);

  it('creates a private exercise with the muscle groups in canonical order', async () => {
    libraryHandlers({ exercises: libraryPage([]) });
    let body: unknown = null;
    server.use(
      http.post(`${API}/exercises`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(exercise(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Novo exercício' }));
    await user.type(screen.getByLabelText('Nome'), 'Supino inclinado');
    await user.click(screen.getByRole('combobox', { name: 'Grupos musculares' }));
    await user.click(await screen.findByRole('option', { name: 'Tríceps' }));
    await user.click(screen.getByRole('option', { name: 'Peito' }));
    await user.keyboard('{Escape}');
    expect(screen.getByRole('button', { name: 'Remover Tríceps' })).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Criar exercício' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toEqual({
      name: 'Supino inclinado',
      description: null,
      muscle_groups: 'chest,triceps',
      equipment: null,
      difficulty_level: null,
      video_url: null,
    });
    expect(toastMock.success).toHaveBeenCalledWith('Exercício criado com sucesso.');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  }, 15000);

  it('edits a private exercise and sends back the hidden external video link', async () => {
    libraryHandlers({
      exercises: libraryPage([exercise({ video_url: 'https://videos.example/remada' })]),
    });
    let body: Record<string, unknown> | null = null;
    server.use(
      http.patch(`${API}/exercises/${EXERCISE_ID}`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(exercise({ name: 'Remada unilateral' }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Editar Remada curvada' }));
    expect(screen.getByRole('button', { name: 'Remover Costas' })).toBeVisible();
    expect(screen.queryByLabelText(/ligação|url/i)).toBeNull();
    const name = screen.getByLabelText('Nome');
    await user.clear(name);
    await user.type(name, 'Remada unilateral');
    await user.click(screen.getByRole('button', { name: 'Guardar alterações' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({
      name: 'Remada unilateral',
      muscle_groups: 'back,biceps',
      difficulty_level: 'intermediate',
      video_url: 'https://videos.example/remada',
    });
    expect(toastMock.success).toHaveBeenCalledWith('Exercício atualizado com sucesso.');
  }, 15000);

  it('puts server validation errors on the fields, in Portuguese', async () => {
    libraryHandlers({ exercises: libraryPage([]) });
    server.use(
      http.post(`${API}/exercises`, () =>
        HttpResponse.json(
          problem('validation_failed', {
            status: 400,
            errors: [
              {
                field: 'Name',
                code: 'exercise_name_too_long',
                message: "'Name' must be 255 characters or fewer.",
              },
            ],
          }),
          { status: 400 }
        )
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Novo exercício' }));
    await user.type(screen.getByLabelText('Nome'), 'Agachamento');
    await user.click(screen.getByRole('button', { name: 'Criar exercício' }));

    expect(await screen.findByText('O nome não pode exceder 255 caracteres.')).toBeVisible();
    expect(screen.getByLabelText('Nome')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.queryByText(/must be 255/)).toBeNull();
  }, 15000);

  it('validates the required supplement fields before calling the API', async () => {
    libraryHandlers({ supplements: libraryPage([]) });
    let posted = 0;
    server.use(
      http.post(`${API}/supplements`, () => {
        posted += 1;
        return HttpResponse.json(supplement(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=supplements`] });

    await user.click(await screen.findByRole('button', { name: 'Novo suplemento' }));
    await user.click(screen.getByRole('button', { name: 'Criar suplemento' }));

    expect(await screen.findByText('Indica o nome do suplemento.')).toBeVisible();
    expect(screen.getByText('Indica a dose.')).toBeVisible();
    expect(screen.getByText('Indica a unidade de medida.')).toBeVisible();
    expect(screen.getByText('Indica quando tomar.')).toBeVisible();
    expect(posted).toBe(0);
  }, 15000);

  it('checks the macros per 100 g and sends decimals written with a comma', async () => {
    libraryHandlers({ foods: libraryPage([]) });
    let body: unknown = null;
    server.use(
      http.post(`${API}/foods`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(food(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=foods`] });

    await user.click(await screen.findByRole('button', { name: 'Novo alimento' }));
    await user.type(screen.getByLabelText('Nome'), 'Frango grelhado');
    await user.type(screen.getByLabelText('Proteína (g)'), '60');
    await user.type(screen.getByLabelText('Hidratos (g)'), '30');
    await user.type(screen.getByLabelText('Gordura (g)'), '20');
    await user.click(screen.getByRole('button', { name: 'Criar alimento' }));
    expect(
      await screen.findByText('Proteína, hidratos e gordura não podem somar mais de 100 g.')
    ).toBeVisible();
    expect(body).toBeNull();

    const fats = screen.getByLabelText('Gordura (g)');
    await user.clear(fats);
    await user.type(fats, '2,5');
    expect(screen.getByText('≈ 382,5 kcal por 100 g')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Porção habitual (g)'), '150,25');
    await user.click(screen.getByRole('button', { name: 'Criar alimento' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toEqual({
      name: 'Frango grelhado',
      description: null,
      protein: 60,
      carbs: 30,
      fats: 2.5,
      fiber: null,
      default_serving_grams: 150.25,
    });
  }, 15000);

  it('archives a private item after saying what happens to existing plans', async () => {
    libraryHandlers();
    let archived = false;
    server.use(
      http.post(`${API}/exercises/${EXERCISE_ID}/archive`, () => {
        archived = true;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Arquivar Remada curvada' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'Arquivar exercício?' });
    expect(dialog).toHaveTextContent(
      'Remada curvada deixa de aparecer ao criar planos novos. Os planos que já o usam não mudam.'
    );
    await user.click(within(dialog).getByRole('button', { name: 'Arquivar' }));

    await waitFor(() => expect(archived).toBe(true));
    expect(toastMock.success).toHaveBeenCalledWith('Remada curvada foi arquivado com sucesso.');
  }, 15000);

  it('opens an archived private item read-only and offers to reactivate it', async () => {
    libraryHandlers({ supplements: libraryPage([supplement({ is_active: false })]) });
    let reactivated = false;
    server.use(
      http.post(`${API}/supplements/${SUPPLEMENT_ID}/reactivate`, () => {
        reactivated = true;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=supplements&activity=archived`] });

    const row = await rowOf('Multivitamínico');
    expect(within(row).getByText('Arquivado')).toBeInTheDocument();
    expect(within(row).queryByRole('button', { name: /^Editar/ })).toBeNull();
    await user.click(within(row).getByRole('button', { name: 'Ver Multivitamínico' }));
    expect(
      await screen.findByText(/Arquivado: reativa-o na lista para o voltares a editar/)
    ).toBeVisible();
    expect(screen.queryByRole('button', { name: 'Guardar alterações' })).toBeNull();
    await user.keyboard('{Escape}');

    await user.click(within(row).getByRole('button', { name: 'Reativar Multivitamínico' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'Reativar suplemento?' });
    await user.click(within(dialog).getByRole('button', { name: 'Reativar' }));
    await waitFor(() => expect(reactivated).toBe(true));
  }, 15000);

  it('shows why a blocked item cannot be edited', async () => {
    libraryHandlers({
      foods: libraryPage([
        food({
          platform_enforcement_status: 'blocked',
          platform_enforcement_reason: 'dangerous_information',
        }),
      ]),
    });
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=foods`] });

    const row = await rowOf('Batido pós-treino');
    expect(within(row).getByText('Bloqueado')).toBeInTheDocument();
    expect(within(row).getByRole('button', { name: 'Arquivar Batido pós-treino' })).toBeVisible();
    await user.click(within(row).getByRole('button', { name: 'Ver Batido pós-treino' }));

    expect(await screen.findByRole('note')).toHaveTextContent(
      'Bloqueado pela moderação da plataforma. Motivo: Informação perigosa.'
    );
    expect(screen.queryByLabelText('Proteína (g)')).toBeNull();
  }, 15000);

  it('plays the video of a global exercise through the trainer route, without upload', async () => {
    libraryHandlers();
    server.use(
      http.get(`${API}/exercises/${GLOBAL_EXERCISE_ID}/video`, () =>
        HttpResponse.json({
          video_id: '99999999-9999-9999-9999-999999999999',
          exercise_id: GLOBAL_EXERCISE_ID,
          content_type: 'video/mp4',
          duration_milliseconds: 20000,
          width: 1280,
          height: 720,
          playback_url: 'https://storage.test/playback/agachamento.mp4',
          expires_at: '2026-09-29T12:30:00Z',
        })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Ver Agachamento com barra' }));
    expect(await screen.findByText('Vídeo disponível')).toBeVisible();
    expect(screen.queryByLabelText('Enviar vídeo')).toBeNull();
    expect(screen.queryByLabelText('Substituir vídeo')).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Ver vídeo' }));

    expect(await screen.findByLabelText('Vídeo de Agachamento com barra')).toHaveAttribute(
      'src',
      'https://storage.test/playback/agachamento.mp4'
    );
  }, 15000);

  it('uploads the video of a private exercise through the trainer routes', async () => {
    libraryHandlers();
    const storage = stubStorage();
    stubVideoMetadata();
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
    const calls: string[] = [];
    server.use(
      http.post(`${API}/exercises/${EXERCISE_ID}/video/uploads`, () => {
        calls.push('request');
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
      http.get(`${API}/exercises/${EXERCISE_ID}/video/uploads/:videoId`, () => {
        calls.push('status');
        return HttpResponse.json(video('ready'));
      })
    );
    const user = userEvent.setup({ applyAccept: false });
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Editar Remada curvada' }));
    await user.upload(
      screen.getByLabelText('Enviar vídeo'),
      new File(['video-bytes'], 'remada.mp4', { type: 'video/mp4' })
    );

    expect(await screen.findByText('Vídeo pronto.')).toBeInTheDocument();
    expect(calls).toEqual(['request', 'complete', 'status']);
    expect(storage).toHaveLength(1);
    expect(storage[0]!.headers).toEqual({ 'content-type': 'video/mp4' });
  }, 15000);

  it('tells an empty library apart from a search without results', async () => {
    libraryHandlers({ exercises: libraryPage([]) });
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Ainda não há exercícios')).toBeVisible();
    expect(screen.getByRole('button', { name: 'Criar exercício' })).toBeVisible();

    await user.type(screen.getByRole('searchbox', { name: 'Pesquisar exercícios' }), 'zzz');
    expect(await screen.findByText('Sem resultados')).toBeVisible();
    await user.click(screen.getByRole('button', { name: 'Limpar filtros' }));

    // Apagar a pesquisa não espera pelo debounce (300 ms): o estado vazio chega antes.
    expect(await screen.findByText('Ainda não há exercícios', {}, { timeout: 200 })).toBeVisible();
    expect(window.location.search).toBe('');
  }, 15000);

  it('treats a page past the last one as a filtered result, not an empty library', async () => {
    libraryHandlers({ foods: libraryPage([], 30, 3) });
    renderApp({ initialEntries: [`${ROUTE}?tab=foods&page=3`] });

    expect(await screen.findByText('Sem resultados')).toBeVisible();
    expect(screen.queryByText('Ainda não há alimentos')).toBeNull();
  }, 15000);

  it('shows the error state and retries the list', async () => {
    let attempts = 0;
    server.use(
      ...restorableSession(),
      http.get(`${API}/exercises`, () => {
        attempts += 1;
        return attempts === 1
          ? HttpResponse.json(problem('internal_error', { status: 500 }), { status: 500 })
          : HttpResponse.json(libraryPage([exercise()]));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Tentar novamente' }));

    expect(await rowOf('Remada curvada')).toBeInTheDocument();
  }, 15000);

  it('sends archive and reactivate to the route of the open tab', async () => {
    libraryHandlers({ foods: libraryPage([food()]) });
    let archivedFood = false;
    server.use(
      http.post(`${API}/foods/${FOOD_ID}/archive`, () => {
        archivedFood = true;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=foods`] });

    await user.click(await screen.findByRole('button', { name: 'Arquivar Batido pós-treino' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'Arquivar alimento?' });
    await user.click(within(dialog).getByRole('button', { name: 'Arquivar' }));

    await waitFor(() => expect(archivedFood).toBe(true));
  }, 15000);
  it('switching tab and typing at once never sends the previous tab search', async () => {
    const urls = libraryHandlers();
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=foods&search=batido`] });
    await rowOf('Batido pós-treino');

    await user.click(screen.getByRole('tab', { name: 'Suplementos' }));
    await user.type(screen.getByRole('searchbox', { name: 'Pesquisar suplementos' }), 'mu');

    await waitFor(() =>
      expect(urls.supplements.some((url) => url.searchParams.get('search') === 'mu')).toBe(true)
    );
    expect(urls.supplements.map((url) => url.searchParams.get('search'))).not.toContain('batido');
  }, 15000);

  it('shows the error of an invalid macro instead of the sum error', async () => {
    libraryHandlers({ foods: libraryPage([]) });
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=foods`] });

    await user.click(await screen.findByRole('button', { name: 'Novo alimento' }));
    await user.type(screen.getByLabelText('Nome'), 'Frango');
    await user.type(screen.getByLabelText('Proteína (g)'), 'abc');
    await user.type(screen.getByLabelText('Hidratos (g)'), '0');
    await user.type(screen.getByLabelText('Gordura (g)'), '0');
    await user.click(screen.getByRole('button', { name: 'Criar alimento' }));

    expect(await screen.findByText('A proteína tem de estar entre 0 e 100 g.')).toBeVisible();
    expect(
      screen.queryByText('Proteína, hidratos e gordura não podem somar mais de 100 g.')
    ).toBeNull();
  }, 15000);

  it('plays the new video after a replacement, not the cached signed URL', async () => {
    libraryHandlers({
      exercises: libraryPage([exercise({ has_ready_video: true, managed_video_status: 'ready' })]),
    });
    stubStorage();
    stubVideoMetadata();
    const playbackUrls = [
      'https://storage.test/playback/old.mp4',
      'https://storage.test/playback/new.mp4',
    ];
    let playbacks = 0;
    const video = (status: string) => ({
      id: '99999999-9999-9999-9999-999999999997',
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
    server.use(
      http.get(`${API}/exercises/${EXERCISE_ID}/video`, () => {
        const url = playbackUrls[Math.min(playbacks, 1)]!;
        playbacks += 1;
        return HttpResponse.json({
          video_id: video('ready').id,
          exercise_id: EXERCISE_ID,
          content_type: 'video/mp4',
          duration_milliseconds: 20000,
          width: 1280,
          height: 720,
          playback_url: url,
          expires_at: '2026-09-29T12:30:00Z',
        });
      }),
      http.post(`${API}/exercises/${EXERCISE_ID}/video/uploads`, () =>
        HttpResponse.json(
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
        )
      ),
      http.post(`${API}/exercises/${EXERCISE_ID}/video/uploads/:videoId/complete`, () =>
        HttpResponse.json(video('processing'))
      ),
      http.get(`${API}/exercises/${EXERCISE_ID}/video/uploads/:videoId`, () =>
        HttpResponse.json(video('ready'))
      )
    );
    const user = userEvent.setup({ applyAccept: false });
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Editar Remada curvada' }));
    await user.click(screen.getByRole('button', { name: 'Ver vídeo' }));
    expect(await screen.findByLabelText('Vídeo de Remada curvada')).toHaveAttribute(
      'src',
      playbackUrls[0]
    );
    await user.upload(
      screen.getByLabelText('Substituir vídeo'),
      new File(['video-bytes'], 'remada.mp4', { type: 'video/mp4' })
    );
    expect(await screen.findByText('Vídeo pronto.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Ver vídeo' }));

    expect(await screen.findByLabelText('Vídeo de Remada curvada')).toHaveAttribute(
      'src',
      playbackUrls[1]
    );
    expect(playbacks).toBe(2);
  }, 15000);

  // [Fecho 6E-3] A soma dos macros em vírgula flutuante (0,15 + 65,01 + 34,84 = 100,00000000000001)
  // recusava um alimento que o backend aceita (decimal): a regra compara em centésimas.
  it('accepts macros that add up to exactly 100 g with decimals', async () => {
    libraryHandlers({ foods: libraryPage([]) });
    let body: unknown = null;
    server.use(
      http.post(`${API}/foods`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(food(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [`${ROUTE}?tab=foods`] });

    await user.click(await screen.findByRole('button', { name: 'Novo alimento' }));
    await user.type(screen.getByLabelText('Nome'), 'Açúcar mascavado');
    await user.type(screen.getByLabelText('Proteína (g)'), '0,15');
    await user.type(screen.getByLabelText('Hidratos (g)'), '65,01');
    await user.type(screen.getByLabelText('Gordura (g)'), '34,84');
    await user.click(screen.getByRole('button', { name: 'Criar alimento' }));

    await waitFor(() => expect(body).not.toBeNull());
    expect(body).toMatchObject({ protein: 0.15, carbs: 65.01, fats: 34.84 });
    expect(
      screen.queryByText('Proteína, hidratos e gordura não podem somar mais de 100 g.')
    ).toBeNull();
  }, 15000);

  // [Fecho 6E-3] O painel de vídeo pode fechar-se durante o processamento: a lista acompanha
  // sozinha o estado até ficar terminal, e depois deixa de repetir o pedido.
  it('refreshes the video column while a video is processing, then stops', async () => {
    let calls = 0;
    server.use(
      ...restorableSession(),
      http.get(`${API}/exercises`, () => {
        calls += 1;
        const ready = calls > 1;
        return HttpResponse.json(
          libraryPage([
            exercise({
              has_ready_video: ready,
              managed_video_status: ready ? 'ready' : 'processing',
            }),
          ])
        );
      })
    );
    renderApp({ initialEntries: [ROUTE] });

    const row = await rowOf('Remada curvada');
    expect(within(row).queryByText('Com vídeo')).toBeNull();
    await waitFor(() => expect(within(row).getByText('Com vídeo')).toBeInTheDocument(), {
      timeout: 5000,
    });
    const afterReady = calls;
    await new Promise((resolve) => setTimeout(resolve, 3500));
    expect(calls).toBe(afterReady);
  }, 20000);
});
