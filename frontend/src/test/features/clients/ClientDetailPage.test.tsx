import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import {
  CLIENT_ID,
  clientDetails,
  clientPack,
  clientPackPage,
  clientSummaryOverview,
  initialAssessment,
  sessionPage,
  trainingSession,
} from '@/test/msw/trainer-fixtures';
import { trainerSettings } from '@/test/msw/account-fixtures';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const ROUTE = `/trainer/clients/${CLIENT_ID}`;
const INVITE_URL = 'https://app.example.test/accept-invite?token=tok-123';

type Invitation = { state: string; email: string | null; expires_at: string | null };
const NO_INVITATION: Invitation = { state: 'none', email: null, expires_at: null };

/** Handlers do detalhe; devolve o contador de pedidos à avaliação inicial. */
function detailHandlers(
  details = clientDetails(),
  summary = clientSummaryOverview(),
  invitation: Invitation = NO_INVITATION
): { assessmentRequests: () => number } {
  let assessmentRequests = 0;
  server.use(
    ...restorableSession(),
    http.get(`${API}/clients/:clientId`, () => HttpResponse.json(details)),
    http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(summary)),
    http.get(`${API}/clients/:clientId/invitation`, () => HttpResponse.json(invitation)),
    http.get(`${API}/clients/:clientId/initial-assessment`, () => {
      assessmentRequests += 1;
      return HttpResponse.json(problem('resource_not_found'), { status: 404 });
    })
  );
  return { assessmentRequests: () => assessmentRequests };
}

describe('ClientDetailPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('shows header and summary KPIs and reads the assessment once', async () => {
    const { assessmentRequests } = detailHandlers();
    renderApp({ initialEntries: [ROUTE] });

    // Primeiro render do ficheiro: carrega o chunk lazy e o shell (lento em máquinas ocupadas).
    expect(
      await screen.findByRole('heading', { name: 'Marta Figueiredo' }, { timeout: 12000 })
    ).toBeInTheDocument();
    expect(screen.getByText('Pack 10 sessões · 3 restantes')).toBeInTheDocument();
    expect(await screen.findByText('86 %')).toBeInTheDocument();
    expect(screen.getByText('96 de 112 séries (28 dias)')).toBeInTheDocument();
    expect(screen.getByText('-1,4 kg desde 22/07/2026')).toBeInTheDocument();
    expect(screen.getByText('180 g HC · 140 g P · 70 g G')).toBeInTheDocument();
    expect(screen.getByText('fim previsto 30/09/2026')).toBeInTheDocument();
    expect(screen.getByText('válido até 30/09/2026')).toBeInTheDocument();
    expect(screen.getByText(/64,8 kg · 168 cm · cliente desde 04\/02\/2026/)).toBeInTheDocument();
    await waitFor(() => expect(assessmentRequests()).toBe(1));
  }, 15000);

  it('warns that the client has no initial assessment, and stays quiet when there is one', async () => {
    detailHandlers();
    const { unmount } = renderApp({ initialEntries: [ROUTE] });

    expect(
      await screen.findByText(/Sem avaliação inicial\. O cliente preenche-a no portal/)
    ).toHaveTextContent(
      'Até lá não podes criar planos, atribuir suplementos nem marcar avaliações periódicas.'
    );
    unmount();

    detailHandlers();
    server.use(
      http.get(`${API}/clients/:clientId/initial-assessment`, () =>
        HttpResponse.json(initialAssessment())
      )
    );
    renderApp({ initialEntries: [ROUTE] });

    await screen.findByRole('heading', { name: 'Marta Figueiredo' });
    await waitFor(() => expect(screen.getByRole('tab', { name: 'Resumo' })).toBeInTheDocument());
    expect(screen.queryByText(/Sem avaliação inicial\./)).not.toBeInTheDocument();
  }, 15000);

  it('does not ask for the assessment of an archived client', async () => {
    const { assessmentRequests } = detailHandlers(clientDetails({ is_active: false }));
    renderApp({ initialEntries: [ROUTE] });

    await screen.findByRole('heading', { name: 'Marta Figueiredo' });
    expect(assessmentRequests()).toBe(0);
    expect(screen.queryByText(/Sem avaliação inicial\./)).not.toBeInTheDocument();
  }, 15000);

  it('says what archiving does before asking to confirm', async () => {
    detailHandlers();
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Arquivar cliente' }));
    const dialog = await screen.findByRole('alertdialog');

    expect(within(dialog).getByText(/as sessões futuras são canceladas/)).toBeInTheDocument();
    expect(
      within(dialog).getByText(/o saldo que resta nos packs é dado como consumido/)
    ).toBeInTheDocument();
    expect(within(dialog).getByText(/o acesso ao portal termina já/)).toBeInTheDocument();
    expect(within(dialog).getByText(/o convite por aceitar deixa de servir/)).toBeInTheDocument();
    expect(
      within(dialog).getByText(
        /quem já tinha conta volta a entrar, mas as sessões canceladas, o saldo consumido e o convite anulado não voltam/
      )
    ).toBeInTheDocument();
  }, 15000);

  it('keeps the short text when reactivating', async () => {
    detailHandlers(clientDetails({ is_active: false }));
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Reativar cliente' }));
    const dialog = await screen.findByRole('alertdialog');

    expect(
      within(dialog).getByText(
        'Marta Figueiredo volta às listas ativas e conta para o limite do plano.'
      )
    ).toBeInTheDocument();
    expect(within(dialog).queryByText(/sessões futuras/)).not.toBeInTheDocument();
  }, 15000);

  it('links to the editor pages instead of opening side panels', async () => {
    detailHandlers();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByRole('link', { name: /Editar ficha/ })).toHaveAttribute(
      'href',
      `${ROUTE}/edit`
    );
    expect(screen.getByRole('link', { name: /Avaliação inicial/ })).toHaveAttribute(
      'href',
      `${ROUTE}/initial-assessment`
    );
    expect(screen.getByRole('tab', { name: 'Avaliações' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Check-ins' })).not.toBeInTheDocument();
  }, 15000);

  it('keeps name and status badge in the same header row, with a non-italic avatar initial', async () => {
    detailHandlers();
    renderApp({ initialEntries: [ROUTE] });

    const heading = await screen.findByRole('heading', { name: 'Marta Figueiredo' });
    const badge = screen.getByText('Ativo');
    expect(heading.parentElement).toContainElement(badge);
    expect(heading.parentElement).toHaveClass('items-center');
    expect(screen.getByText('MF')).not.toHaveClass('font-display');
  }, 15000);

  it('shows explicit texts for missing summary data instead of zeros', async () => {
    detailHandlers(
      clientDetails({ usable_packs: [] }),
      clientSummaryOverview({
        weight: null,
        height_cm: null,
        training_plan: null,
        adherence: null,
        nutrition: null,
        packs: { usable_pack_count: 0, sessions_remaining: 0, next_expected_end_date: null },
      })
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Sem registos de peso')).toBeInTheDocument();
    expect(screen.getByText('Sem plano de treino ativo')).toBeInTheDocument();
    expect(screen.getByText('Sem plano alimentar ativo')).toBeInTheDocument();
    expect(screen.getByText('Sem packs ativos')).toBeInTheDocument();
    expect(screen.getByText('Este cliente não tem plano ativo.')).toBeInTheDocument();
  }, 15000);

  it('reports a client from another tenant as not found', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients/:clientId`, () =>
        HttpResponse.json(problem('client_not_found'), { status: 404 })
      ),
      http.get(`${API}/clients/:clientId/summary`, () =>
        HttpResponse.json(problem('client_not_found'), { status: 404 })
      )
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByText('Cliente não encontrado.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Marta Figueiredo' })).not.toBeInTheDocument();
  }, 15000);

  /** Convite aceite pelo servidor: devolve o link e a validade (U61). */
  function inviteHandler(onBody?: (body: unknown) => void, emailSent = true) {
    server.use(
      http.post(`${API}/auth/invite-client`, async ({ request }) => {
        onBody?.(await request.json());
        return HttpResponse.json({
          invite_url: INVITE_URL,
          expires_at: '2026-10-28T14:30:00Z',
          email_sent: emailSent,
        });
      })
    );
  }

  async function sendInvite(user: ReturnType<typeof userEvent.setup>, label = 'Enviar convite') {
    await user.click(
      await screen.findByRole('button', { name: /Convidar para o portal|Enviar outro convite/ })
    );
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', { name: label })
    );
    return screen.findByRole('dialog');
  }

  it('invites with the contact email and shows the sent dialog without writing the link', async () => {
    let body: unknown = null;
    detailHandlers();
    inviteHandler((value) => (body = value));
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    const dialog = await sendInvite(user);

    await waitFor(() => expect(body).toEqual({ client_id: CLIENT_ID, email: 'marta@example.com' }));
    expect(
      within(dialog).getByText(/Convite enviado para marta@example.com\./)
    ).toBeInTheDocument();
    // 14:30 UTC é 14:30 em Lisboa a 28/10/2026 (o horário de verão acabou a 25/10).
    expect(within(dialog).getByText(/Válido até 28\/10\/2026 às 14:30/)).toBeInTheDocument();
    expect(screen.queryByText(INVITE_URL)).not.toBeInTheDocument();
    expect(screen.queryByDisplayValue(INVITE_URL)).not.toBeInTheDocument();
  }, 15000);

  // O email pode falhar com o convite já criado: o link serve e tem de ir por outro canal.
  it('says the email did not go out and still offers the link', async () => {
    detailHandlers();
    inviteHandler(undefined, false);
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    const dialog = await sendInvite(user);

    expect(within(dialog).getByRole('heading', { name: 'Convite criado' })).toBeInTheDocument();
    expect(
      within(dialog).getByText(/O email para marta@example.com não saiu agora./)
    ).toBeInTheDocument();
    expect(within(dialog).getByRole('alert')).toHaveTextContent(/o link funciona/);
    expect(within(dialog).getByRole('button', { name: 'Copiar link' })).toBeInTheDocument();
  }, 15000);

  it('copies the invite link and confirms with «Copiado»', async () => {
    detailHandlers();
    inviteHandler();
    const user = userEvent.setup();
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue(undefined);
    renderApp({ initialEntries: [ROUTE] });

    const dialog = await sendInvite(user);
    await user.click(within(dialog).getByRole('button', { name: 'Copiar link' }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(INVITE_URL));
    expect(await within(dialog).findByRole('button', { name: 'Copiado' })).toBeInTheDocument();
    expect(within(dialog).getByRole('status')).toHaveTextContent('Link copiado');
  }, 15000);

  it('forgets the link once the dialog is closed', async () => {
    detailHandlers();
    inviteHandler();
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    const dialog = await sendInvite(user);
    await user.click(within(dialog).getByRole('button', { name: 'Fechar' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Copiar link' })).not.toBeInTheDocument();
  }, 15000);

  it('shows the link in a read-only field when the clipboard fails', async () => {
    detailHandlers();
    inviteHandler();
    const user = userEvent.setup();
    vi.spyOn(navigator.clipboard, 'writeText').mockRejectedValue(new Error('denied'));
    renderApp({ initialEntries: [ROUTE] });

    const dialog = await sendInvite(user);
    await user.click(within(dialog).getByRole('button', { name: 'Copiar link' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Não foi possível copiar');
    const field = within(dialog).getByRole('textbox', { name: 'Link do convite' });
    expect(field).toHaveValue(INVITE_URL);
    expect(field).toHaveAttribute('readonly');
    expect(within(dialog).getByRole('button', { name: 'Copiar link' })).toBeInTheDocument();
  }, 15000);

  describe('invitation state on the record', () => {
    const pending = {
      state: 'pending',
      email: 'marta@example.com',
      expires_at: '2026-10-28T14:30:00Z',
    };

    it('shows the validity of a pending invite', async () => {
      detailHandlers(clientDetails(), clientSummaryOverview(), pending);
      renderApp({ initialEntries: [ROUTE] });

      expect(
        await screen.findByText('Convite enviado · válido até 28/10/2026 às 14:30')
      ).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Convidar para o portal/ })).toBeInTheDocument();
    }, 15000);

    it('uses the trainer time zone, not the browser one', async () => {
      detailHandlers(clientDetails(), clientSummaryOverview(), {
        ...pending,
        expires_at: '2026-07-10T22:30:00Z',
      });
      server.use(
        http.get(`${API}/trainer-settings`, () =>
          HttpResponse.json(trainerSettings({ timezone: 'America/Sao_Paulo' }))
        )
      );
      renderApp({ initialEntries: [ROUTE] });

      // São Paulo é UTC-3: 22:30 UTC são 19:30 locais.
      expect(
        await screen.findByText('Convite enviado · válido até 10/07/2026 às 19:30')
      ).toBeInTheDocument();
    }, 15000);

    it('offers another invite when the previous one expired', async () => {
      detailHandlers(clientDetails(), clientSummaryOverview(), {
        ...pending,
        state: 'expired',
        expires_at: '2026-10-10T09:05:00Z',
      });
      renderApp({ initialEntries: [ROUTE] });

      // Em Lisboa a 10/10 vigora o horário de verão (UTC+1).
      expect(
        await screen.findByText('O convite expirou a 10/10/2026 às 10:05')
      ).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Enviar outro convite' })).toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: /Convidar para o portal/ })
      ).not.toBeInTheDocument();
    }, 15000);

    it('says which email the invite went to when the record email changed', async () => {
      detailHandlers(
        clientDetails({ contact_email: 'marta.nova@example.com' }),
        clientSummaryOverview(),
        { ...pending, email: 'marta.antiga@example.com' }
      );
      renderApp({ initialEntries: [ROUTE] });

      expect(
        await screen.findByText(
          'Convite enviado para marta.antiga@example.com · válido até 28/10/2026 às 14:30'
        )
      ).toBeInTheDocument();
    }, 15000);

    it.each([
      ['accepted', { state: 'accepted', email: null, expires_at: null }],
      ['none', NO_INVITATION],
    ])(
      'shows nothing about the invite when it is %s',
      async (_, invitation) => {
        detailHandlers(clientDetails(), clientSummaryOverview(), invitation);
        renderApp({ initialEntries: [ROUTE] });

        expect(
          await screen.findByRole('heading', { name: 'Marta Figueiredo' })
        ).toBeInTheDocument();
        await screen.findByText('86 %');
        expect(screen.queryByText(/Convite enviado|O convite expirou/)).not.toBeInTheDocument();
      },
      15000
    );

    it('refreshes the invite state after sending another invite', async () => {
      let state: Invitation = {
        state: 'expired',
        email: 'marta@example.com',
        expires_at: '2026-10-10T09:05:00Z',
      };
      detailHandlers();
      server.use(
        http.get(`${API}/clients/:clientId/invitation`, () => HttpResponse.json(state)),
        http.post(`${API}/auth/invite-client`, () => {
          state = pending;
          return HttpResponse.json({
            invite_url: INVITE_URL,
            expires_at: pending.expires_at,
            email_sent: true,
          });
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: [ROUTE] });

      expect(await screen.findByText(/O convite expirou a/)).toBeInTheDocument();
      await sendInvite(user, 'Enviar outro convite');

      expect(
        await screen.findByText('Convite enviado · válido até 28/10/2026 às 14:30')
      ).toBeInTheDocument();
    }, 15000);
  });

  // Sem conta, o botão fica visível mas desativado e diz o que falta, em vez de desaparecer.
  it.each([
    [
      'no contact email',
      { contact_email: null },
      'Adiciona um email de contacto na ficha para o convidar para o portal.',
    ],
    [
      'an archived record',
      { is_active: false },
      'Reativa o cliente para o convidar para o portal.',
    ],
  ])(
    'disables the invite and explains why for %s',
    async (_, overrides, reason) => {
      detailHandlers(clientDetails(overrides));
      renderApp({ initialEntries: [ROUTE] });

      const button = await screen.findByRole('button', { name: /Convidar para o portal/ });
      expect(button).toBeDisabled();
      expect(button).toHaveAccessibleDescription(new RegExp(reason));
    },
    15000
  );

  it.each([['a portal account', { user_id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' }]])(
    'hides the invite for %s',
    async (_, overrides) => {
      detailHandlers(clientDetails(overrides));
      renderApp({ initialEntries: [ROUTE] });

      expect(await screen.findByRole('heading', { name: 'Marta Figueiredo' })).toBeInTheDocument();
      expect(
        screen.queryByRole('button', { name: /Convidar para o portal/ })
      ).not.toBeInTheDocument();
    },
    15000
  );

  it('translates an invite refused by the email delivery', async () => {
    detailHandlers();
    server.use(
      http.post(`${API}/auth/invite-client`, () =>
        HttpResponse.json(problem('authentication_email_delivery_unavailable'), { status: 503 })
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: /Convidar para o portal/ }));
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Enviar convite',
      })
    );

    await waitFor(() =>
      expect(toastMock.error).toHaveBeenCalledWith(
        'O email de convite não pôde ser enviado agora. Tenta mais tarde.'
      )
    );
  }, 15000);

  // [6E2] NOVO: separador "Sessões" (packs e sessões do cliente), sincronizado com `?tab=`.
  describe('Sessões tab', () => {
    /** Sessões e packs do cliente; devolve os URLs pedidos a cada lista. */
    function sessionsTabHandlers(): { sessionUrls: URL[]; packUrls: URL[] } {
      const sessionUrls: URL[] = [];
      const packUrls: URL[] = [];
      server.use(
        http.get(`${API}/sessions`, ({ request }) => {
          sessionUrls.push(new URL(request.url));
          return HttpResponse.json(sessionPage([trainingSession()]));
        }),
        http.get(`${API}/client-session-packs`, ({ request }) => {
          packUrls.push(new URL(request.url));
          return HttpResponse.json(clientPackPage([clientPack()]));
        })
      );
      return { sessionUrls, packUrls };
    }

    it('does not load sessions or packs while the summary tab is open', async () => {
      detailHandlers();
      const { sessionUrls, packUrls } = sessionsTabHandlers();
      renderApp({ initialEntries: [ROUTE] });

      expect(await screen.findByText('86 %')).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: 'Resumo' })).toHaveAttribute('aria-selected', 'true');
      expect(sessionUrls).toEqual([]);
      expect(packUrls).toEqual([]);
    }, 15000);

    it("opens from the URL and lists only this client's sessions and packs", async () => {
      detailHandlers();
      const { sessionUrls, packUrls } = sessionsTabHandlers();
      renderApp({ initialEntries: [`${ROUTE}?tab=sessions`] });

      expect(await screen.findByText('3 de 10 restantes')).toBeInTheDocument();
      expect(await screen.findByText('PT individual · Estúdio A · 60 min')).toBeInTheDocument();
      expect(sessionUrls[0]?.searchParams.get('client_id')).toBe(CLIENT_ID);
      expect(packUrls[0]?.searchParams.get('client_id')).toBe(CLIENT_ID);
      // A coluna Cliente seria redundante no próprio cliente.
      expect(screen.queryByRole('columnheader', { name: 'Cliente' })).not.toBeInTheDocument();
      expect(screen.getByRole('link', { name: /Marcar sessão/ })).toBeInTheDocument();
    }, 15000);

    it('writes the selected tab to the URL', async () => {
      detailHandlers();
      sessionsTabHandlers();
      const user = userEvent.setup();
      renderApp({ initialEntries: [ROUTE] });

      await user.click(await screen.findByRole('tab', { name: 'Sessões' }));

      await waitFor(() => expect(window.location.search).toBe('?tab=sessions'));
      expect(await screen.findByText('3 de 10 restantes')).toBeInTheDocument();
    }, 15000);

    it('hides "Marcar sessão" for an archived client', async () => {
      detailHandlers(clientDetails({ is_active: false }));
      sessionsTabHandlers();
      renderApp({ initialEntries: [`${ROUTE}?tab=sessions`] });

      expect(await screen.findByText('3 de 10 restantes')).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: /Marcar sessão/ })).not.toBeInTheDocument();
    }, 15000);
  });
});
