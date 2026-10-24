import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { toast } from 'sonner';
import type * as Sonner from 'sonner';
import { describe, expect, it, vi } from 'vitest';

import { portalKeys } from '@/features/portal';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { portalHome } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: { success: vi.fn(), error: vi.fn() },
}));

const STATUS = `${API}/portal/my-initial-assessment`;
const ROUTE = '/portal/initial-assessment';

const pending = (value: boolean) => http.get(STATUS, () => HttpResponse.json({ pending: value }));

/** Abre o ecrã de preenchimento como cliente, com a avaliação por entregar. */
function openWizard(initial = true) {
  server.use(...restorableSession({ role: 'client' }), pending(initial));
  return renderApp({ initialEntries: [ROUTE] });
}

const next = () => screen.getByRole('button', { name: 'Seguinte' });
/** "Enviar avaliação" só fica ativo um instante depois de o último passo abrir. */
async function send() {
  const button = screen.getByRole('button', { name: 'Enviar avaliação' });
  await waitFor(() => expect(button).toBeEnabled());
  return button;
}

/** Preenche o primeiro passo com valores válidos. */
async function fillBody(user: ReturnType<typeof userEvent.setup>, weight = '64.8') {
  await user.type(await screen.findByLabelText('Peso (kg)'), weight);
  await user.type(screen.getByLabelText('Altura (cm)'), '168');
  await user.selectOptions(screen.getByLabelText('Condição física'), 'intermediate');
  await user.selectOptions(screen.getByLabelText('Nível de atividade'), 'moderately_active');
  await user.type(screen.getByLabelText('Objetivos'), 'Recomposição corporal');
}

async function reachLastStep(user: ReturnType<typeof userEvent.setup>, weight?: string) {
  await fillBody(user, weight);
  await user.click(next());
  await screen.findByRole('heading', { name: 'Medidas' });
  await user.click(next());
  await screen.findByRole('heading', { name: 'Hábitos e saúde' });
}

describe('PortalInitialAssessmentPage', () => {
  it('walks the three steps and sends the exact request, then goes back to the home', async () => {
    let body: unknown = null;
    let posts = 0;
    server.use(
      http.post(STATUS, async ({ request }) => {
        posts += 1;
        body = await request.json();
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    const { router } = openWizard();

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Avaliação inicial' })
    ).toBeVisible();
    expect(screen.getByRole('heading', { name: 'Corpo e objetivos' })).toBeInTheDocument();
    await fillBody(user);
    await user.click(next());
    expect(await screen.findByRole('heading', { name: 'Medidas' })).toHaveFocus();
    await user.type(screen.getByLabelText('Cintura (cm)'), '80.5');
    await user.click(next());
    expect(await screen.findByRole('heading', { name: 'Hábitos e saúde' })).toHaveFocus();
    await user.type(screen.getByLabelText('Água por dia (L)'), '2.5');
    await user.type(screen.getByLabelText('Alergias'), 'Amendoim');

    await user.click(await send());

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal'));
    expect(toast.success).toHaveBeenCalledWith('Avaliação inicial enviada.');
    expect(posts).toBe(1);
    expect(body).toEqual({
      weight_kg: 64.8,
      height_cm: 168,
      body_fat_percentage: null,
      medical_conditions: null,
      fitness_level: 'intermediate',
      activity_level: 'moderately_active',
      goals: 'Recomposição corporal',
      profession: null,
      body_measurements: {
        waist_cm: 80.5,
        hip_cm: null,
        chest_cm: null,
        right_arm_cm: null,
        left_arm_cm: null,
        right_thigh_cm: null,
        left_thigh_cm: null,
        right_calf_cm: null,
        left_calf_cm: null,
      },
      nutrition_intake: {
        food_preferences: null,
        disliked_foods: null,
        food_intolerances: null,
        food_allergies: 'Amendoim',
        dietary_restrictions: null,
        daily_routine: null,
        sleep_quality: null,
        mood: null,
        stress_level: null,
        avg_water_liters_per_day: 2.5,
        hungriest_time_of_day: null,
        uses_supplements: null,
        current_supplements: null,
        other_notes: null,
      },
    });
  }, 40000);

  it('goes back to the home even when the status already says delivered before the send resolves', async () => {
    // Como na app real: depois do POST o estado passa a `pending: false`, e o refetch chega
    // antes de o envio terminar (a mutation espera pela invalidação).
    let delivered = false;
    server.use(
      ...restorableSession({ role: 'client' }),
      http.get(STATUS, () => HttpResponse.json({ pending: !delivered })),
      http.post(STATUS, () => {
        delivered = true;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [ROUTE] });

    await reachLastStep(user);
    await user.click(await send());

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal'));
    expect(screen.queryByText(/já foi entregue/)).not.toBeInTheDocument();
  }, 40000);

  it('does not send when "Seguinte" is tapped twice on the second step', async () => {
    let posts = 0;
    server.use(
      http.post(STATUS, () => {
        posts += 1;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    openWizard();

    await fillBody(user);
    await user.click(next());
    await screen.findByRole('heading', { name: 'Medidas' });
    const tapped = next();
    await user.click(tapped);
    await screen.findByRole('heading', { name: 'Hábitos e saúde' });
    const sendNow = screen.getByRole('button', { name: 'Enviar avaliação' });
    expect(tapped).not.toBeInTheDocument();
    expect(sendNow).toBeDisabled();
    await user.click(sendNow);

    expect(posts).toBe(0);
    expect(screen.getByRole('heading', { name: 'Hábitos e saúde' })).toBeInTheDocument();
  }, 40000);

  it('refuses 1000.01 kg and more than two decimals in the first step', async () => {
    const user = userEvent.setup();
    openWizard();

    await fillBody(user, '1000.01');
    await user.click(next());
    expect(await screen.findByText('O peso não pode exceder 1000 kg.')).toBeInTheDocument();
    expect(screen.getByLabelText('Peso (kg)')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('heading', { name: 'Corpo e objetivos' })).toBeInTheDocument();

    const weight = screen.getByLabelText('Peso (kg)');
    await user.clear(weight);
    await user.type(weight, '64.855');
    await user.click(next());
    expect(await screen.findByText('Usa no máximo duas casas decimais.')).toBeInTheDocument();
  }, 40000);

  it('does not preselect the fitness and activity levels', async () => {
    const user = userEvent.setup();
    openWizard();

    expect(await screen.findByLabelText('Condição física')).toHaveValue('');
    await user.click(next());
    expect(await screen.findByText('Indica o peso.')).toBeInTheDocument();
    expect(screen.getByText('Escolhe a condição física.')).toBeInTheDocument();
    expect(screen.getByText('Escolhe o nível de atividade.')).toBeInTheDocument();
    expect(screen.getByText('Indica os objetivos.')).toBeInTheDocument();
  }, 40000);

  it('takes the client back to the first step when the server refuses the weight', async () => {
    server.use(
      http.post(STATUS, () =>
        HttpResponse.json(
          problem('validation_failed', {
            errors: [{ field: 'WeightKg', code: 'weight_invalid', message: 'x' }],
          }),
          { status: 400 }
        )
      )
    );
    const user = userEvent.setup();
    openWizard();

    await reachLastStep(user);
    await user.click(await send());

    expect(await screen.findByRole('heading', { name: 'Corpo e objetivos' })).toBeInTheDocument();
    expect(screen.getByLabelText('Peso (kg)')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('Peso (kg)')).toHaveValue(64.8);
    expect(screen.getByText(/Indica um peso maior que 0 e até 1000 kg/)).toBeInTheDocument();
  }, 40000);

  it('says the assessment was already delivered on a 409 and links to the home', async () => {
    server.use(
      http.post(STATUS, () =>
        HttpResponse.json(problem('initial_assessment_already_exists'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    openWizard();

    await reachLastStep(user);
    await user.click(await send());

    expect(await screen.findByText('A tua avaliação inicial já foi entregue.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Voltar ao início' })).toHaveAttribute(
      'href',
      '/portal'
    );
    expect(screen.queryByRole('form', { name: 'Avaliação inicial' })).not.toBeInTheDocument();
  }, 40000);

  it('shows only the delivered message when the status is not pending', async () => {
    openWizard(false);

    expect(await screen.findByText('A tua avaliação inicial já foi entregue.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Peso (kg)')).not.toBeInTheDocument();
  }, 40000);

  it('shows the unavailable portal for an archived record', async () => {
    server.use(
      ...restorableSession({ role: 'client' }),
      http.get(STATUS, () =>
        HttpResponse.json(problem('portal_profile_not_available'), { status: 404 })
      )
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Portal indisponível')).toBeInTheDocument();
  }, 40000);

  it('asks before leaving with something written', async () => {
    const user = userEvent.setup();
    const { router } = openWizard();

    await user.type(await screen.findByLabelText('Peso (kg)'), '70');
    await act(() => router.navigate('/portal'));

    expect(await screen.findByRole('alertdialog')).toHaveTextContent('Sair sem enviar?');
    expect(router.state.location.pathname).toBe(ROUTE);
  }, 40000);
});

describe('Initial assessment on the portal home', () => {
  it('shows the card first and follows it to the form', async () => {
    server.use(
      ...restorableSession({ role: 'client' }),
      pending(true),
      http.get(`${API}/portal/home`, () =>
        HttpResponse.json(portalHome({ initial_assessment_pending: true }))
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal'] });

    await screen.findByRole('heading', { name: 'Hoje' });
    const regions = screen.getAllByRole('region');
    expect(regions[0]).toHaveAccessibleName('Avaliação inicial');
    expect(regions[1]).toHaveAccessibleName('O meu treino de hoje');

    await user.click(screen.getByRole('link', { name: 'Preencher avaliação inicial' }));
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Avaliação inicial' })
    ).toBeVisible();
    expect(await screen.findByRole('heading', { name: 'Corpo e objetivos' })).toBeInTheDocument();
  }, 40000);

  it('hides the card once the assessment is delivered', async () => {
    server.use(...restorableSession({ role: 'client' }));
    renderApp({ initialEntries: ['/portal'] });

    await screen.findByRole('heading', { name: 'Hoje' });
    expect(screen.queryByRole('region', { name: 'Avaliação inicial' })).not.toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'O meu treino de hoje' })).toBeInTheDocument();
  }, 40000);
});

describe('Missing initial assessment notice', () => {
  const PAGES = [
    '/portal/today',
    '/portal/plan',
    '/portal/nutrition',
    '/portal/nutrition/planos',
    '/portal/supplements',
    '/portal/check-ins',
  ] as const;

  it.each(PAGES)(
    'is shown on %s while the assessment is missing',
    async (path) => {
      server.use(...restorableSession({ role: 'client' }), pending(true));
      renderApp({ initialEntries: [path] });

      expect(await screen.findByText(/Falta a tua avaliação inicial\./)).toHaveTextContent(
        'O teu personal trainer só prepara o planeamento depois de a preencheres.'
      );
      expect(screen.getByRole('link', { name: 'Preencher avaliação inicial' })).toHaveAttribute(
        'href',
        ROUTE
      );
    },
    40000
  );

  it.each(PAGES)(
    'is not shown on %s once it was delivered',
    async (path) => {
      server.use(...restorableSession({ role: 'client' }), pending(false));
      const { queryClient } = renderApp({ initialEntries: [path] });

      await waitFor(() =>
        expect(queryClient.getQueryState(portalKeys.initialAssessment())?.status).toBe('success')
      );
      // Dá tempo ao ecrã de reagir à resposta.
      await screen.findByRole('heading', { level: 1 });
      expect(screen.queryByText(/Falta a tua avaliação inicial\./)).not.toBeInTheDocument();
    },
    40000
  );
});
