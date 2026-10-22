import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { portalKeys } from '@/features/portal';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { DUE_CHECK_IN_ID, dueCheckIn, nextCheckIn, portalHome } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const DUE = `${API}/portal/my-check-ins/due`;
const NEXT = `${API}/portal/my-check-ins/next`;
const RESPOND = `${API}/portal/check-ins/:checkInId/respond`;

/** O backend responde assim quando não há check-in por responder ou agendado. */
const absent = () => HttpResponse.json(problem('resource_not_found'), { status: 404 });

async function openCheckIns() {
  server.use(...restorableSession({ role: 'client' }));
  const view = renderApp({ initialEntries: ['/portal/check-ins'] });
  await screen.findByRole('heading', { name: 'Avaliação periódica' });
  return view;
}

const field = (name: string) => screen.getByRole('textbox', { name });
const next = () => screen.getByRole('button', { name: 'Seguinte' });
const previous = () => screen.getByRole('button', { name: 'Anterior' });
/** "Enviar avaliação" só fica ativo um instante depois de o último passo abrir. */
async function send() {
  const button = screen.getByRole('button', { name: 'Enviar avaliação' });
  await waitFor(() => expect(button).toBeEnabled());
  return button;
}

/** Preenche o peso e avança até ao último passo. */
async function reachLastStep(user: ReturnType<typeof userEvent.setup>, weight = '71,5') {
  await user.type(field('Peso (kg)'), weight);
  await user.click(next());
  await screen.findByRole('heading', { name: 'Medidas' });
  await user.click(next());
  await screen.findByRole('heading', { name: 'Como correu' });
}

describe('PortalCheckInsPage', () => {
  beforeEach(() => {
    toastMock.error.mockClear();
  });

  it("opens today's check-in on the first of three steps, with one request", async () => {
    const calls: string[] = [];
    server.use(
      http.get(DUE, () => {
        calls.push('due');
        return HttpResponse.json(dueCheckIn());
      }),
      http.get(NEXT, () => {
        calls.push('next');
        return HttpResponse.json(nextCheckIn());
      })
    );

    await openCheckIns();

    expect(screen.getByText('Domingo, 04/10/2026')).toBeInTheDocument();
    expect(screen.getByText('Só podes responder hoje.')).toBeInTheDocument();
    expect(screen.getByText('Passo 1 de 3')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Peso' })).toBeInTheDocument();
    expect(screen.getByRole('progressbar', { name: 'Progresso da avaliação' })).toHaveAttribute(
      'aria-valuenow',
      '33'
    );
    // Nada vem pré-preenchido, nem o peso.
    expect(field('Peso (kg)')).toHaveValue('');
    expect(field('Massa gorda (%) (opcional)')).toHaveValue('');
    expect(screen.queryByRole('button', { name: 'Anterior' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enviar avaliação' })).not.toBeInTheDocument();
    // Com um check-in por responder, o próximo seria esse mesmo: não se pede.
    expect(calls).toEqual(['due']);
  });

  it('does not leave a step while it has a problem', async () => {
    const user = userEvent.setup();
    await openCheckIns();

    await user.click(next());

    expect(await screen.findByText('Indica o peso.')).toBeInTheDocument();
    expect(field('Peso (kg)')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('heading', { name: 'Peso' })).toBeInTheDocument();

    await user.type(field('Peso (kg)'), '71,5');
    await user.type(field('Massa gorda (%) (opcional)'), '100');
    await user.click(next());

    expect(
      await screen.findByText('Indica uma percentagem maior que 0 e menor que 100.')
    ).toBeInTheDocument();
    expect(screen.queryByText('Indica o peso.')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Peso' })).toBeInTheDocument();

    await user.clear(field('Massa gorda (%) (opcional)'));
    await user.click(next());
    await screen.findByRole('heading', { name: 'Medidas' });

    await user.type(field('Cintura (cm)'), 'oitenta');
    await user.click(next());

    expect(
      await screen.findByText('Indica uma medida maior que 0 e até 1000 cm.')
    ).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Medidas' })).toBeInTheDocument();
  });

  it('keeps what was typed across the steps, sends it and then shows the next check-in', async () => {
    let answered = false;
    let body: unknown = null;
    let release: () => void = () => undefined;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.get(DUE, () => (answered ? absent() : HttpResponse.json(dueCheckIn()))),
      http.post(RESPOND, async ({ request, params }) => {
        expect(params.checkInId).toBe(DUE_CHECK_IN_ID);
        body = await request.json();
        await held;
        answered = true;
        return HttpResponse.json(
          dueCheckIn({ status: 'answered', weight_kg: 71.5, responded_at: '2026-10-04T10:00:00Z' })
        );
      })
    );
    const user = userEvent.setup();
    const { queryClient, router } = await openCheckIns();
    // A home em cache prova que o envio a invalida (o cartão resume o check-in).
    queryClient.setQueryData(portalKeys.home(), portalHome());

    await user.type(field('Peso (kg)'), '71,5');
    await user.type(field('Massa gorda (%) (opcional)'), '17,25');
    await user.click(next());

    expect(await screen.findByText('Passo 2 de 3')).toBeInTheDocument();
    const stepTitle = screen.getByRole('heading', { name: 'Medidas' });
    // O foco vai para o título do passo, para o leitor de ecrã anunciar a mudança.
    await waitFor(() => expect(stepTitle).toHaveFocus());
    await user.type(field('Cintura (cm)'), '82,5');
    await user.click(next());

    expect(await screen.findByText('Passo 3 de 3')).toBeInTheDocument();
    await user.type(field('Energia'), 'Alta toda a semana.');
    await user.type(field('Adesão ao treino (%)'), '0');
    await user.type(field('Adesão à alimentação (%)'), '85');
    await user.type(field('Notas'), 'Semana boa.');

    // Recuar e avançar não perde nada.
    await user.click(previous());
    expect(await screen.findByRole('textbox', { name: 'Cintura (cm)' })).toHaveValue('82,5');
    await user.click(previous());
    expect(await screen.findByRole('textbox', { name: 'Peso (kg)' })).toHaveValue('71,5');
    await user.click(next());
    await screen.findByRole('heading', { name: 'Medidas' });
    await user.click(next());
    expect(await screen.findByRole('textbox', { name: 'Energia' })).toHaveValue(
      'Alta toda a semana.'
    );

    await user.click(await send());

    // Espera pelo servidor: o botão fica desativado e o formulário continua no ecrã.
    const sending = await screen.findByRole('button', { name: 'A enviar…' });
    expect(sending).toBeDisabled();
    expect(previous()).toBeDisabled();

    release();

    expect(
      await screen.findByText('Avaliação enviada. O teu personal trainer já a pode ver.')
    ).toBeInTheDocument();
    expect(body).toEqual({
      weight_kg: 71.5,
      body_fat_percentage: 17.25,
      notes: 'Semana boa.',
      body_measurements: {
        waist_cm: 82.5,
        hip_cm: null,
        chest_cm: null,
        right_arm_cm: null,
        left_arm_cm: null,
        right_thigh_cm: null,
        left_thigh_cm: null,
        right_calf_cm: null,
        left_calf_cm: null,
      },
      feedback: {
        appetite: null,
        digestion: null,
        training_load: null,
        recovery_sleep: null,
        energy_levels: 'Alta toda a semana.',
        body_response: null,
      },
      training_adherence_score: 0,
      nutrition_adherence_score: 85,
    });
    expect(screen.queryByRole('form', { name: 'Responder à avaliação' })).not.toBeInTheDocument();
    // O próximo vem do segundo pedido; os valores enviados não se repetem (não há histórico).
    expect(await screen.findByRole('heading', { name: 'Próxima avaliação' })).toBeInTheDocument();
    expect(
      screen.getByText('Domingo, 18/10/2026. Poderás responder nesse dia.')
    ).toBeInTheDocument();
    expect(screen.queryByText(/71,5/)).not.toBeInTheDocument();
    expect(queryClient.getQueryState(portalKeys.home())?.isInvalidated).toBe(true);
    expect(toastMock.error).not.toHaveBeenCalled();

    // Depois de enviado já não há nada por guardar: sair não pergunta.
    await user.click(screen.getByRole('link', { name: 'Treino' }));
    await waitFor(() => expect(router.state.location.pathname).toBe('/portal/today'));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('sends only the weight when the rest is left empty', async () => {
    let body: unknown = null;
    server.use(
      http.post(RESPOND, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(dueCheckIn({ status: 'answered', weight_kg: 80 }));
      })
    );
    const user = userEvent.setup();
    await openCheckIns();

    await reachLastStep(user, '80');
    await user.click(await send());

    await screen.findByText('Avaliação enviada. O teu personal trainer já a pode ver.');
    expect(body).toEqual({
      weight_kg: 80,
      body_fat_percentage: null,
      notes: null,
      body_measurements: null,
      feedback: null,
      training_adherence_score: null,
      nutrition_adherence_score: null,
    });
  });

  it('does not send the evaluation when "Seguinte" is tapped twice on the second step', async () => {
    let posts = 0;
    server.use(
      http.post(RESPOND, () => {
        posts += 1;
        return HttpResponse.json(dueCheckIn({ status: 'answered' }));
      })
    );
    const user = userEvent.setup();
    await openCheckIns();

    await user.type(field('Peso (kg)'), '71,5');
    await user.click(next());
    await screen.findByRole('heading', { name: 'Medidas' });
    // O segundo toque cai no mesmo sítio do ecrã, já depois de o passo mudar.
    const tapped = next();
    await user.click(tapped);
    await screen.findByRole('heading', { name: 'Como correu' });
    // "Seguinte" e "Enviar" são nós diferentes, e o de enviar começa desativado.
    expect(tapped).not.toBeInTheDocument();
    const sendNow = screen.getByRole('button', { name: 'Enviar avaliação' });
    expect(sendNow).toBeDisabled();
    await user.click(sendNow);

    expect(posts).toBe(0);
    expect(screen.getByRole('heading', { name: 'Como correu' })).toBeInTheDocument();
    expect(
      screen.queryByText('Avaliação enviada. O teu personal trainer já a pode ver.')
    ).not.toBeInTheDocument();
  });

  it('says why the send button is disabled for the first moments of the last step', async () => {
    const user = userEvent.setup();
    await openCheckIns();

    await user.type(field('Peso (kg)'), '71,5');
    await user.click(next());
    await screen.findByRole('heading', { name: 'Medidas' });
    await user.click(next());
    await screen.findByRole('heading', { name: 'Como correu' });

    const button = screen.getByRole('button', { name: 'Enviar avaliação' });
    expect(button).toBeDisabled();
    expect(button).toHaveAccessibleDescription(
      'Revê as respostas deste passo. Já podes enviar daqui a instantes.'
    );

    // Passado o tempo, o botão ativa-se e o motivo sai.
    await waitFor(() => expect(button).toBeEnabled());
    expect(button).not.toHaveAccessibleDescription();
    expect(screen.queryByText(/Já podes enviar daqui a instantes/)).not.toBeInTheDocument();
  });

  it('tells the client the evaluation is no longer available when it disappears mid-way', async () => {
    let available = true;
    server.use(
      http.get(DUE, () => (available ? HttpResponse.json(dueCheckIn()) : absent())),
      http.get(NEXT, absent)
    );
    const user = userEvent.setup();
    const { queryClient } = await openCheckIns();
    await user.type(field('Peso (kg)'), '71,5');

    // O dia passou: a releitura já não a devolve.
    available = false;
    await queryClient.invalidateQueries({ queryKey: portalKeys.checkInDue() });

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Esta avaliação já não está disponível para responder. O dia pode ter passado.'
    );
    expect(screen.queryByRole('form', { name: 'Responder à avaliação' })).not.toBeInTheDocument();
    expect(
      await screen.findByRole('heading', { name: 'Sem avaliações agendadas' })
    ).toBeInTheDocument();
  });

  it('does not warn when an untouched evaluation disappears', async () => {
    let available = true;
    server.use(
      http.get(DUE, () => (available ? HttpResponse.json(dueCheckIn()) : absent())),
      http.get(NEXT, absent)
    );
    const { queryClient } = await openCheckIns();

    available = false;
    await queryClient.invalidateQueries({ queryKey: portalKeys.checkInDue() });

    expect(
      await screen.findByRole('heading', { name: 'Sem avaliações agendadas' })
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('never shows the word "check-in" in the page, in any of its states', async () => {
    const user = userEvent.setup();
    const { queryClient } = await openCheckIns();
    const visible = () => {
      const main = screen.getByRole('main');
      const labels = [...main.querySelectorAll('[aria-label]')].map((node) =>
        node.getAttribute('aria-label')
      );
      return [main.textContent, ...labels].join(' ');
    };

    expect(visible()).not.toMatch(/check-?in/i);
    await user.type(field('Peso (kg)'), '71,5');
    await user.click(next());
    await screen.findByRole('heading', { name: 'Medidas' });
    await user.click(next());
    await screen.findByRole('heading', { name: 'Como correu' });
    expect(visible()).not.toMatch(/check-?in/i);

    server.use(
      http.get(DUE, absent),
      http.get(NEXT, () => HttpResponse.json(nextCheckIn()))
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.checkInDue() });
    await screen.findByRole('heading', { name: 'Próxima avaliação' });
    expect(visible()).not.toMatch(/check-?in/i);
  });

  it('checks the last step before sending', async () => {
    let posts = 0;
    server.use(
      http.post(RESPOND, () => {
        posts += 1;
        return HttpResponse.json(dueCheckIn({ status: 'answered' }));
      })
    );
    const user = userEvent.setup();
    await openCheckIns();

    await reachLastStep(user);
    await user.type(field('Adesão ao treino (%)'), '101');
    await user.click(await send());

    expect(await screen.findByText('Indica um valor inteiro de 0 a 100.')).toBeInTheDocument();
    expect(field('Adesão ao treino (%)')).toHaveAttribute('aria-invalid', 'true');
    expect(posts).toBe(0);
  });

  it('takes the client back to the step of a field the server refused, and lets them resend', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.post(RESPOND, async ({ request }) => {
        bodies.push(await request.json());
        if (bodies.length === 1)
          // Corpo real de um 400: o código está em `errors[]` e a medida vem pela posição.
          return HttpResponse.json(
            {
              ...problem('validation_failed'),
              errors: [
                {
                  field: 'BodyMeasurements[2]',
                  code: 'body_measurement_invalid',
                  message: "'' não verifica a condição definida.",
                },
              ],
            },
            { status: 400 }
          );
        return HttpResponse.json(dueCheckIn({ status: 'answered' }));
      })
    );
    const user = userEvent.setup();
    await openCheckIns();

    await user.type(field('Peso (kg)'), '71,5');
    await user.click(next());
    await screen.findByRole('heading', { name: 'Medidas' });
    await user.type(field('Peito (cm)'), '100');
    await user.click(next());
    await screen.findByRole('heading', { name: 'Como correu' });
    await user.click(await send());

    // Volta ao passo das medidas, com a mensagem no campo certo e o valor ainda lá.
    expect(await screen.findByRole('heading', { name: 'Medidas' })).toBeInTheDocument();
    expect(field('Peito (cm)')).toHaveAttribute('aria-invalid', 'true');
    expect(field('Peito (cm)')).toHaveValue('100');
    expect(field('Cintura (cm)')).toHaveAttribute('aria-invalid', 'false');
    expect(screen.getByText('Indica uma medida maior que 0 e até 1000 cm.')).toBeInTheDocument();
    expect(toastMock.error).not.toHaveBeenCalled();

    await user.clear(field('Peito (cm)'));
    await user.type(field('Peito (cm)'), '101');
    await user.click(next());
    await screen.findByRole('heading', { name: 'Como correu' });
    await user.click(await send());

    await screen.findByText('Avaliação enviada. O teu personal trainer já a pode ver.');
    expect(bodies).toHaveLength(2);
    expect(bodies[1]).toMatchObject({ body_measurements: { chest_cm: 101 } });
  });

  it.each([
    ['check_in_already_answered', 'Esta avaliação já foi respondida.'],
    ['check_in_wrong_day', 'Esta avaliação só podia ser respondida no próprio dia.'],
    ['check_in_cancelled', 'Esta avaliação foi cancelada pelo teu personal trainer.'],
  ])('explains %s in a toast and rereads the screen', async (code, message) => {
    let refused = false;
    server.use(
      // Depois da recusa o check-in já não está por responder.
      http.get(DUE, () => (refused ? absent() : HttpResponse.json(dueCheckIn()))),
      http.post(RESPOND, () => {
        refused = true;
        return HttpResponse.json(problem(code), { status: 409 });
      })
    );
    const user = userEvent.setup();
    await openCheckIns();

    await reachLastStep(user);
    await user.click(await send());

    await waitFor(() => expect(toastMock.error).toHaveBeenCalledWith(message));
    // O formulário sai (a mensagem não podia viver nele) e o ecrã mostra o próximo.
    expect(await screen.findByRole('heading', { name: 'Próxima avaliação' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enviar avaliação' })).not.toBeInTheDocument();
    expect(screen.queryByText(/Avaliação enviada/)).not.toBeInTheDocument();
  });

  it('goes back to the earliest step when the server refuses fields of several steps', async () => {
    server.use(
      http.post(RESPOND, () =>
        HttpResponse.json(
          {
            ...problem('validation_failed'),
            errors: [
              { field: 'Feedback[0]', code: 'check_in_feedback_too_long', message: '' },
              { field: 'WeightKg', code: 'weight_invalid', message: '' },
            ],
          },
          { status: 400 }
        )
      )
    );
    const user = userEvent.setup();
    await openCheckIns();

    await reachLastStep(user);
    await user.click(await send());

    expect(await screen.findByRole('heading', { name: 'Peso' })).toBeInTheDocument();
    expect(field('Peso (kg)')).toHaveAttribute('aria-invalid', 'true');
    // O erro do último passo fica à espera: aparece quando o cliente lá voltar.
    await user.click(next());
    await screen.findByRole('heading', { name: 'Medidas' });
    await user.click(next());
    await screen.findByRole('heading', { name: 'Como correu' });
  });

  it('keeps everything typed when the server fails, and says so', async () => {
    server.use(
      http.post(RESPOND, () => HttpResponse.json(problem('Internal server error'), { status: 500 }))
    );
    const user = userEvent.setup();
    await openCheckIns();

    await reachLastStep(user);
    await user.type(field('Notas'), 'Semana boa.');
    await user.click(await send());

    expect(
      await screen.findByText(
        'Não foi possível enviar a avaliação. O que escreveste continua aqui.'
      )
    ).toBeInTheDocument();
    expect(field('Notas')).toHaveValue('Semana boa.');
    // É uma falha do envio, não de um campo: nenhum fica marcado.
    expect(field('Notas')).toHaveAttribute('aria-invalid', 'false');
    expect(await send()).toBeEnabled();
    expect(toastMock.error).not.toHaveBeenCalled();
    await user.click(previous());
    await user.click(await screen.findByRole('button', { name: 'Anterior' }));
    expect(await screen.findByRole('textbox', { name: 'Peso (kg)' })).toHaveValue('71,5');
  });

  it('shows the next check-in when there is none today, without an error or a retry', async () => {
    const calls: string[] = [];
    server.use(
      http.get(DUE, () => {
        calls.push('due');
        return absent();
      }),
      http.get(NEXT, () => {
        calls.push('next');
        return HttpResponse.json(nextCheckIn());
      })
    );

    await openCheckIns();

    expect(await screen.findByRole('heading', { name: 'Próxima avaliação' })).toBeInTheDocument();
    expect(
      screen.getByText('Domingo, 18/10/2026. Poderás responder nesse dia.')
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
    expect(screen.queryByText('Só podes responder hoje.')).not.toBeInTheDocument();
    expect(screen.queryByText('Passo 1 de 3')).not.toBeInTheDocument();
    expect(calls).toEqual(['due', 'next']);
  });

  it('says so when nothing is scheduled', async () => {
    server.use(http.get(DUE, absent), http.get(NEXT, absent));

    await openCheckIns();

    expect(
      await screen.findByRole('heading', { name: 'Sem avaliações agendadas' })
    ).toBeInTheDocument();
    expect(
      screen.getByText('Quando o teu personal trainer agendar uma avaliação, aparece aqui.')
    ).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('treats a technical failure as an error, with retry', async () => {
    let fail = true;
    server.use(
      ...restorableSession({ role: 'client' }),
      http.get(DUE, () =>
        fail
          ? HttpResponse.json(problem('Internal server error'), { status: 500 })
          : HttpResponse.json(dueCheckIn())
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/check-ins'] });

    const alert = await screen.findByRole('alert');
    expect(within(alert).getByText('Não foi possível carregar.')).toBeInTheDocument();
    fail = false;
    await user.click(within(alert).getByRole('button', { name: 'Tentar novamente' }));

    expect(await screen.findByText('Passo 1 de 3')).toBeInTheDocument();
  });

  it('lets the client retry when only the next check-in fails to load', async () => {
    let fail = true;
    server.use(
      http.get(DUE, absent),
      http.get(NEXT, () =>
        fail
          ? HttpResponse.json(problem('Internal server error'), { status: 500 })
          : HttpResponse.json(nextCheckIn())
      )
    );
    const user = userEvent.setup();
    await openCheckIns();

    expect(
      await screen.findByText('Não foi possível saber quando é a tua próxima avaliação.')
    ).toBeInTheDocument();
    fail = false;
    await user.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    expect(await screen.findByRole('heading', { name: 'Próxima avaliação' })).toBeInTheDocument();
  });

  it('keeps the form and warns when a refresh fails', async () => {
    const user = userEvent.setup();
    const { queryClient } = await openCheckIns();
    await user.type(field('Peso (kg)'), '71,5');

    server.use(
      http.get(DUE, () => HttpResponse.json(problem('Internal server error'), { status: 500 }))
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.checkInDue() });

    expect(
      await screen.findByText(
        'Não foi possível atualizar. Estás a ver os últimos dados carregados.'
      )
    ).toBeInTheDocument();
    expect(field('Peso (kg)')).toHaveValue('71,5');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('asks before leaving with something typed, and stays when the client cancels', async () => {
    const user = userEvent.setup();
    const { router } = await openCheckIns();
    await user.type(field('Peso (kg)'), '71,5');

    await user.click(screen.getByRole('link', { name: 'Treino' }));

    // Fechar o separador ou recarregar também avisa (o browser mostra o seu próprio diálogo).
    const closing = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(closing);
    expect(closing.defaultPrevented).toBe(true);

    const dialog = await screen.findByRole('alertdialog', { name: 'Sair sem enviar?' });
    expect(
      within(dialog).getByText('O que escreveste nesta avaliação não fica guardado.')
    ).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Cancelar' }));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe('/portal/check-ins');
    expect(field('Peso (kg)')).toHaveValue('71,5');

    await user.click(screen.getByRole('link', { name: 'Treino' }));
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Sair sem enviar',
      })
    );

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal/today'));
    expect(await screen.findByRole('heading', { name: 'Treino de hoje' })).toBeInTheDocument();
  });

  it('does not ask before leaving an untouched form', async () => {
    const user = userEvent.setup();
    const { router } = await openCheckIns();
    await screen.findByText('Passo 1 de 3');

    const closing = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(closing);
    expect(closing.defaultPrevented).toBe(false);

    await user.click(screen.getByRole('link', { name: 'Treino' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/portal/today'));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('never holds a sign-out, even with something typed', async () => {
    const user = userEvent.setup();
    const { router } = await openCheckIns();
    await user.type(field('Peso (kg)'), '71,5');

    screen.getByRole('button', { name: 'Menu de perfil' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Terminar sessão' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/entrar'));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('sends a trainer away from the client check-ins', async () => {
    let requests = 0;
    server.use(
      ...restorableSession({ role: 'trainer' }),
      http.get(DUE, () => {
        requests += 1;
        return HttpResponse.json(dueCheckIn());
      })
    );
    const { router } = renderApp({ initialEntries: ['/portal/check-ins'] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer'));
    expect(screen.queryByText('Passo 1 de 3')).not.toBeInTheDocument();
    expect(requests).toBe(0);
  });
});
