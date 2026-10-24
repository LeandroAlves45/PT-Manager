import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { describe, expect, it, vi } from 'vitest';

import type { components } from '@/shared/api/schema';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

type Trainer = components['schemas']['AdminTrainerResponse'];
type Invitation = components['schemas']['PendingTrainerInvitationResponse'];

const TRAINER_ID = '44444444-4444-4444-4444-444444444444';
const EMAIL = 'ana.silva@ptmanager.test';

function trainer(overrides: Partial<Trainer> = {}): Trainer {
  return {
    trainer_id: TRAINER_ID,
    full_name: 'Ana Silva',
    email: EMAIL,
    subscription_status: 'ACTIVE',
    subscription_tier: 'STARTER',
    is_exempt_from_billing: false,
    billing_exempt_since: null,
    has_paid_subscription: false,
    active_client_count: 12,
    created_at: '2026-03-02T12:00:00Z',
    access_block: null,
    ...overrides,
  };
}

function exemptTrainer(overrides: Partial<Trainer> = {}): Trainer {
  return trainer({
    subscription_status: 'ACTIVE',
    subscription_tier: 'PRO',
    is_exempt_from_billing: true,
    billing_exempt_since: '2026-09-14T09:00:00Z',
    ...overrides,
  });
}

function blocked(overrides: Partial<Trainer> = {}): Trainer {
  return trainer({
    access_block: {
      reason: 'malicious_content',
      note: 'Link para um site falso',
      blocked_at: '2026-10-20T09:00:00Z',
      client_access_ends_at: '2026-10-27T09:00:00Z',
    },
    ...overrides,
  });
}

function invitation(overrides: Partial<Invitation> = {}): Invitation {
  return {
    invitation_id: '55555555-5555-5555-5555-555555555555',
    email: 'novo@ptmanager.test',
    sent_at: '2099-01-03T09:00:00Z',
    expires_at: '2099-01-10T09:00:00Z',
    ...overrides,
  };
}

function lookupBody(
  status: 'found' | 'invite_pending' | 'not_found' | 'not_trainer',
  extra: { trainer?: Trainer; invitation?: Invitation } = {}
) {
  return { status, trainer: extra.trainer ?? null, invitation: extra.invitation ?? null };
}

/** Handlers das duas listas; devolvem os pedidos recebidos para os testes os inspecionarem. */
function serveLists(
  options: { exempt?: Trainer[]; total?: number; invitations?: Invitation[] } = {}
) {
  const exempt = options.exempt ?? [];
  const total = options.total ?? exempt.length;
  const invitations = options.invitations ?? [];
  const exemptQueries: URLSearchParams[] = [];
  const state = { exempt, invitations };
  server.use(
    ...restorableSession({ role: 'superuser', trainer_id: null }),
    http.get(`${API}/admin/trainers/exempt`, ({ request }) => {
      exemptQueries.push(new URL(request.url).searchParams);
      return HttpResponse.json({
        items: state.exempt,
        total_count: total,
        page_number: 1,
        page_size: 25,
      });
    }),
    http.get(`${API}/admin/trainer-invitations`, () => HttpResponse.json(state.invitations))
  );
  return { exemptQueries, state };
}

async function search(user: ReturnType<typeof userEvent.setup>, email = EMAIL) {
  await user.type(await screen.findByLabelText('Email do treinador'), email);
  await user.click(screen.getByRole('button', { name: 'Procurar' }));
}

describe('AdminTrainersPage', () => {
  it('shows the header, the invite action and both empty states', async () => {
    serveLists();
    renderApp({ initialEntries: ['/admin/trainers'] });

    expect(await screen.findByRole('heading', { level: 1, name: 'Treinadores' })).toBeVisible();
    expect(
      screen.getByText(
        'Procura um treinador pelo email completo. Isenta de pagamento ou envia um convite isento.'
      )
    ).toBeVisible();
    expect(screen.getByRole('link', { name: 'Convidar treinador' })).toHaveAttribute(
      'href',
      '/admin/trainers/invite'
    );
    expect(await screen.findByText('Ainda não há treinadores isentos.')).toBeVisible();
    expect(await screen.findByText('Não há convites pendentes.')).toBeVisible();
  });

  describe('lookup', () => {
    it('only searches on submit, with the full email in the JSON body, never in the URL', async () => {
      serveLists();
      const emails: (string | null)[] = [];
      const urls: string[] = [];
      server.use(
        http.post(`${API}/admin/trainers/lookup`, async ({ request }) => {
          const body = (await request.json()) as { email?: string };
          emails.push(body.email ?? null);
          urls.push(request.url);
          return HttpResponse.json(lookupBody('found', { trainer: trainer() }));
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      const input = await screen.findByLabelText('Email do treinador');
      expect(input).toHaveAttribute('type', 'email');
      await user.type(input, 'ana.silva');
      expect(emails).toEqual([]);

      await user.type(input, '@ptmanager.test');
      expect(emails).toEqual([]);
      await user.click(screen.getByRole('button', { name: 'Procurar' }));

      expect(await screen.findByRole('heading', { name: 'Ana Silva' })).toBeVisible();
      expect(emails).toEqual([EMAIL]);
      expect(urls[0]).not.toContain('ana');
      expect(new URL(urls[0] ?? '').search).toBe('');
    });

    it('shows the account data of a trainer, and never clients or plans', async () => {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(
            lookupBody('found', {
              trainer: trainer({ subscription_status: 'SUSPENDED', active_client_count: 1 }),
            })
          )
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);

      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(card).getByText(EMAIL)).toBeVisible();
      expect(within(card).getByText('Suspensa por pagamento')).toBeVisible();
      expect(within(card).getByText('Starter')).toBeVisible();
      expect(within(card).getByText('Conta criada a 02/03/2026')).toBeVisible();
      expect(within(card).getByText('1 cliente ativo')).toBeVisible();
      expect(within(card).queryByText('Isento')).not.toBeInTheDocument();
      expect(within(card).getByRole('button', { name: 'Isentar de pagamento' })).toBeVisible();
    });

    it('shows «Sem nome», the badge and the exemption date in Lisbon time', async () => {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(
            lookupBody('found', {
              trainer: exemptTrainer({ full_name: null, active_client_count: 0 }),
            })
          )
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);

      const card = await screen.findByRole('article', { name: EMAIL });
      expect(within(card).getByRole('heading', { name: 'Sem nome' })).toBeVisible();
      expect(within(card).getByText('Isento')).toBeVisible();
      expect(within(card).getByText('0 clientes ativos')).toBeVisible();
      expect(within(card).getByText(/Isento desde 14\/09\/2026.*10:00/)).toBeVisible();
      expect(within(card).getByRole('button', { name: 'Tirar isenção' })).toBeVisible();
    });

    it('asks for a complete valid email on a 400 and on an empty field', async () => {
      serveLists();
      const calls: string[] = [];
      server.use(
        http.post(`${API}/admin/trainers/lookup`, ({ request }) => {
          calls.push(request.url);
          return HttpResponse.json(
            problem('validation_failed', {
              errors: [{ field: 'email', code: 'admin_trainer_email_invalid', message: 'x' }],
            }),
            { status: 400 }
          );
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await user.click(await screen.findByRole('button', { name: 'Procurar' }));
      expect(await screen.findByText('Escreve um email completo e válido.')).toBeVisible();
      expect(calls).toEqual([]);

      await user.type(screen.getByLabelText('Email do treinador'), 'ana');
      await user.click(screen.getByRole('button', { name: 'Procurar' }));

      await waitFor(() => expect(calls).toHaveLength(1));
      expect(await screen.findByText('Escreve um email completo e válido.')).toBeVisible();
    });

    it('sends the email to the invite page through navigation state, not the URL', async () => {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () => HttpResponse.json(lookupBody('not_found')))
      );
      const user = userEvent.setup();
      const { router } = renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user, 'novo@ptmanager.test');
      expect(await screen.findByText('Não encontrámos um treinador com este email.')).toBeVisible();
      await user.click(screen.getByRole('button', { name: 'Enviar convite isento' }));

      await waitFor(() => expect(router.state.location.pathname).toBe('/admin/trainers/invite'));
      expect(router.state.location.search).toBe('');
      expect(router.state.location.hash).toBe('');
      expect(router.state.location.pathname).not.toContain('novo');
      expect(await screen.findByLabelText('Email do treinador')).toHaveValue('novo@ptmanager.test');
    });

    it('announces the lookup result in a status region', async () => {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, async ({ request }) => {
          const body = (await request.json()) as { email?: string };
          return HttpResponse.json(
            body.email === EMAIL
              ? lookupBody('found', { trainer: trainer() })
              : lookupBody('not_found')
          );
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await screen.findByRole('heading', { name: 'Ana Silva' });
      expect(screen.getByText('Treinador encontrado.')).toBeInTheDocument();
      expect(
        screen.getAllByRole('status').some((el) => el.textContent === 'Treinador encontrado.')
      ).toBe(true);

      const input = screen.getByLabelText('Email do treinador');
      await user.clear(input);
      await user.type(input, 'outro@ptmanager.test');
      await user.click(screen.getByRole('button', { name: 'Procurar' }));

      expect(await screen.findByText('Nenhum treinador com este email.')).toBeInTheDocument();
    });

    it('says when the email belongs to an account that is not a trainer', async () => {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(lookupBody('not_trainer'))
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);

      expect(
        await screen.findByText(/Este email já tem uma conta que não é de treinador ativa/)
      ).toBeVisible();
      expect(
        screen.queryByRole('button', { name: 'Isentar de pagamento' })
      ).not.toBeInTheDocument();
    });

    it('shows a pending invitation with its validity, and an expired one as expired', async () => {
      serveLists();
      let expired = false;
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(
            lookupBody('invite_pending', {
              invitation: invitation(
                expired
                  ? { expires_at: '2020-01-10T09:00:00Z' }
                  : { expires_at: '2099-01-10T09:00:00Z' }
              ),
            })
          )
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user, 'novo@ptmanager.test');
      expect(
        await screen.findByText(
          /Convite enviado a novo@ptmanager\.test\. Válido até 10\/01\/2099.*09:00\./
        )
      ).toBeVisible();

      expired = true;
      await user.click(screen.getByRole('button', { name: 'Procurar' }));
      expect(
        await screen.findByText(
          /Convite enviado a novo@ptmanager\.test\. Expirou a 10\/01\/2020.*09:00\./
        )
      ).toBeVisible();
    });

    it('resends the invitation from the result and replaces it with the new one', async () => {
      serveLists();
      const bodies: unknown[] = [];
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(lookupBody('invite_pending', { invitation: invitation() }))
        ),
        http.post(`${API}/admin/trainer-invitations`, async ({ request }) => {
          bodies.push(await request.json());
          return HttpResponse.json(
            invitation({
              invitation_id: '66666666-6666-6666-6666-666666666666',
              expires_at: '2099-02-20T09:00:00Z',
            }),
            { status: 201 }
          );
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user, 'novo@ptmanager.test');
      await user.click(await screen.findByRole('button', { name: 'Enviar outro' }));

      await waitFor(() =>
        expect(toastMock.success).toHaveBeenCalledWith(
          'Convite reenviado. O link anterior deixou de servir.'
        )
      );
      expect(bodies).toEqual([{ email: 'novo@ptmanager.test' }]);
      expect(await screen.findByText(/Válido até 20\/02\/2099.*09:00\./)).toBeVisible();
    });

    it('revokes the invitation from the result after confirming', async () => {
      serveLists();
      const deleted: string[] = [];
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(lookupBody('invite_pending', { invitation: invitation() }))
        ),
        http.delete(`${API}/admin/trainer-invitations/:id`, ({ params }) => {
          deleted.push(String(params['id']));
          return new HttpResponse(null, { status: 204 });
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user, 'novo@ptmanager.test');
      await user.click(await screen.findByRole('button', { name: 'Anular convite' }));
      const dialog = await screen.findByRole('alertdialog');
      expect(
        within(dialog).getByText('O link enviado a novo@ptmanager.test deixa de funcionar.')
      ).toBeVisible();
      await user.click(within(dialog).getByRole('button', { name: 'Anular convite' }));

      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(deleted).toEqual(['55555555-5555-5555-5555-555555555555']);
      expect(screen.queryByText(/Convite enviado a/)).not.toBeInTheDocument();
      expect(toastMock.success).toHaveBeenCalledWith('Convite anulado.');
    });
  });

  describe('billing exemption', () => {
    it('exempts a trainer with a paid subscription, warns about Stripe and updates everything', async () => {
      const lists = serveLists();
      const posted: string[] = [];
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(
            lookupBody('found', { trainer: trainer({ has_paid_subscription: true }) })
          )
        ),
        http.post(`${API}/admin/trainers/:trainerId/billing-exemption`, ({ params }) => {
          posted.push(String(params['trainerId']));
          const updated = exemptTrainer();
          lists.state.exempt = [updated];
          return HttpResponse.json(updated);
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });
      expect(await screen.findByText('Ainda não há treinadores isentos.')).toBeVisible();
      const listCalls = lists.exemptQueries.length;

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Isentar de pagamento' }));

      const dialog = await screen.findByRole('alertdialog');
      expect(
        within(dialog).getByText(
          'Passa ao plano Pro, sem pagamento e sem limite de clientes. Não é enviado nenhum email. A subscrição paga no Stripe é cancelada já, sem reembolso.'
        )
      ).toBeVisible();
      await user.click(within(dialog).getByRole('button', { name: 'Isentar' }));

      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(posted).toEqual([TRAINER_ID]);
      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(card).getByText('Isento')).toBeVisible();
      expect(within(card).getByText('Pro')).toBeVisible();
      expect(within(card).getByRole('button', { name: 'Tirar isenção' })).toBeVisible();
      expect(toastMock.success).toHaveBeenCalledWith('Treinador isento de pagamento.');
      // A lista de isentos foi invalidada e pedida outra vez.
      await waitFor(() => expect(lists.exemptQueries.length).toBeGreaterThan(listCalls));
      expect(await screen.findByRole('rowheader', { name: 'Ana Silva' })).toBeVisible();
    });

    it('does not mention Stripe when there is no paid subscription', async () => {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(lookupBody('found', { trainer: trainer() }))
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Isentar de pagamento' }));

      const dialog = await screen.findByRole('alertdialog');
      expect(
        within(dialog).getByText(
          'Passa ao plano Pro, sem pagamento e sem limite de clientes. Não é enviado nenhum email.'
        )
      ).toBeVisible();
      expect(within(dialog).queryByText(/Stripe/)).not.toBeInTheDocument();

      await user.click(within(dialog).getByRole('button', { name: 'Cancelar' }));
      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    });

    it('removes the exemption from the result card with the Free/inactive warning', async () => {
      const lists = serveLists({ exempt: [exemptTrainer()] });
      const deleted: string[] = [];
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(lookupBody('found', { trainer: exemptTrainer() }))
        ),
        http.delete(`${API}/admin/trainers/:trainerId/billing-exemption`, ({ params }) => {
          deleted.push(String(params['trainerId']));
          lists.state.exempt = [];
          return HttpResponse.json(
            trainer({ subscription_tier: 'FREE', subscription_status: 'INACTIVE' })
          );
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      await user.click(within(card).getByRole('button', { name: 'Tirar isenção' }));

      const dialog = await screen.findByRole('alertdialog');
      expect(
        within(dialog).getByText(
          'Passa ao plano Free, inativo. Não cria nem reativa clientes até pagar uma subscrição.'
        )
      ).toBeVisible();
      await user.click(within(dialog).getByRole('button', { name: 'Tirar isenção' }));

      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(deleted).toEqual([TRAINER_ID]);
      const updated = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(updated).getByText('Free')).toBeVisible();
      expect(within(updated).getByText('Inativa')).toBeVisible();
      expect(within(updated).queryByText('Isento')).not.toBeInTheDocument();
      expect(within(updated).getByRole('button', { name: 'Isentar de pagamento' })).toBeVisible();
      // O treinador sai da lista de isentos depois da invalidação.
      expect(await screen.findByText('Ainda não há treinadores isentos.')).toBeVisible();
    });

    it('keeps the dialog open and explains a Stripe failure with a toast', async () => {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(
            lookupBody('found', { trainer: trainer({ has_paid_subscription: true }) })
          )
        ),
        http.post(`${API}/admin/trainers/:trainerId/billing-exemption`, () =>
          HttpResponse.json(problem('admin_trainer_payment_provider_unavailable'), { status: 503 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Isentar de pagamento' }));
      const dialog = await screen.findByRole('alertdialog');
      await user.click(within(dialog).getByRole('button', { name: 'Isentar' }));

      await waitFor(() =>
        expect(toastMock.error).toHaveBeenCalledWith(
          'Não foi possível cancelar a subscrição no Stripe. Tenta novamente.'
        )
      );
      expect(screen.getByRole('alertdialog')).toBeVisible();
      expect(
        within(screen.getByRole('article', { name: 'Ana Silva', hidden: true })).queryByText(
          'Isento'
        )
      ).not.toBeInTheDocument();
    });

    it('closes the dialog and warns when the trainer no longer exists', async () => {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(lookupBody('found', { trainer: trainer() }))
        ),
        http.post(`${API}/admin/trainers/:trainerId/billing-exemption`, () =>
          HttpResponse.json(problem('admin_trainer_not_found'), { status: 404 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Isentar de pagamento' }));
      await user.click(
        within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Isentar' })
      );

      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(toastMock.error).toHaveBeenCalledWith('Este treinador já não existe.');
    });
  });

  describe('access block', () => {
    function serveLookup(found: Trainer) {
      serveLists();
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(lookupBody('found', { trainer: found }))
        )
      );
    }

    it('shows the block with reason, note and both dates in Lisbon time', async () => {
      serveLookup(blocked());
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);

      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(card).getByText('Bloqueado')).toBeVisible();
      expect(
        within(card).getByText('Motivo: Conteúdo malicioso · Nota: Link para um site falso')
      ).toBeVisible();
      expect(within(card).getByText('Bloqueio')).toBeVisible();
      expect(within(card).getByText(/Bloqueada a 20\/10\/2026.*10:00/)).toBeVisible();
      expect(within(card).getByText(/Clientes com acesso até 27\/10\/2026.*09:00/)).toBeVisible();
      expect(within(card).getByRole('button', { name: 'Levantar bloqueio' })).toBeVisible();
      expect(
        within(card).queryByRole('button', { name: 'Bloquear conta' })
      ).not.toBeInTheDocument();
    });

    it('shows no block information for an account without a block', async () => {
      serveLookup(trainer());
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);

      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(card).queryByText('Bloqueado')).not.toBeInTheDocument();
      expect(within(card).queryByText('Bloqueio')).not.toBeInTheDocument();
      expect(within(card).getByRole('button', { name: 'Bloquear conta' })).toBeVisible();
    });

    it('keeps the exempt badge next to the block badge', async () => {
      serveLookup(
        blocked({ is_exempt_from_billing: true, billing_exempt_since: '2026-09-14T09:00:00Z' })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);

      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(card).getByText('Isento')).toBeVisible();
      expect(within(card).getByText('Bloqueado')).toBeVisible();
    });

    it('needs a reason, sends the exact body and swaps the card for the blocked one', async () => {
      const lists = serveLists();
      const bodies: unknown[] = [];
      const posted: string[] = [];
      server.use(
        http.post(`${API}/admin/trainers/lookup`, () =>
          HttpResponse.json(lookupBody('found', { trainer: trainer() }))
        ),
        http.post(`${API}/admin/trainers/:trainerId/access-block`, async ({ params, request }) => {
          posted.push(String(params['trainerId']));
          bodies.push(await request.json());
          return HttpResponse.json(blocked());
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });
      expect(await screen.findByText('Ainda não há treinadores isentos.')).toBeVisible();
      const listCalls = lists.exemptQueries.length;

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Bloquear conta' }));

      const dialog = await screen.findByRole('dialog');
      expect(
        within(dialog).getByText(/Os clientes com conta continuam a entrar durante 7 dias/)
      ).toBeVisible();
      const confirm = within(dialog).getByRole('button', { name: 'Bloquear conta' });
      expect(confirm).toBeDisabled();
      expect(within(dialog).getAllByRole('radio')).toHaveLength(4);

      await user.click(within(dialog).getByLabelText('Conteúdo malicioso'));
      expect(confirm).toBeEnabled();
      await user.type(within(dialog).getByLabelText('Nota (opcional)'), 'Link para um site falso');
      expect(within(dialog).getByText('23/500')).toBeVisible();
      await user.click(confirm);

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(posted).toEqual([TRAINER_ID]);
      expect(bodies).toEqual([
        { reason_code: 'malicious_content', note: 'Link para um site falso' },
      ]);
      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(card).getByText('Bloqueado')).toBeVisible();
      expect(within(card).getByRole('button', { name: 'Levantar bloqueio' })).toBeVisible();
      expect(toastMock.success).toHaveBeenCalledWith('Conta bloqueada.');
      await waitFor(() => expect(lists.exemptQueries.length).toBeGreaterThan(listCalls));
    });

    it('sends note null when the note is empty or only spaces', async () => {
      const bodies: unknown[] = [];
      serveLookup(trainer());
      server.use(
        http.post(`${API}/admin/trainers/:trainerId/access-block`, async ({ request }) => {
          bodies.push(await request.json());
          return HttpResponse.json(
            blocked({ access_block: { ...blocked().access_block!, note: null } })
          );
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Bloquear conta' }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByLabelText('Informação perigosa'));
      await user.type(within(dialog).getByLabelText('Nota (opcional)'), '   ');
      await user.click(within(dialog).getByRole('button', { name: 'Bloquear conta' }));

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(bodies).toEqual([{ reason_code: 'dangerous_information', note: null }]);
      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(card).getByText('Motivo: Conteúdo malicioso')).toBeVisible();
    });

    it('stops the note at 500 characters', async () => {
      serveLookup(trainer());
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Bloquear conta' }));
      const note = within(await screen.findByRole('dialog')).getByLabelText('Nota (opcional)');
      await user.click(note);
      await user.paste('a'.repeat(520));

      expect(note).toHaveValue('a'.repeat(500));
      expect(screen.getByText('500/500')).toBeVisible();
    });

    it('starts the next block without the reason and note of a cancelled one', async () => {
      serveLookup(trainer());
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Bloquear conta' }));
      let dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByLabelText('Conteúdo proibido'));
      await user.type(within(dialog).getByLabelText('Nota (opcional)'), 'rascunho');
      await user.click(within(dialog).getByRole('button', { name: 'Cancelar' }));
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

      await user.click(screen.getByRole('button', { name: 'Bloquear conta' }));
      dialog = await screen.findByRole('dialog');
      expect(within(dialog).getByLabelText('Conteúdo proibido')).not.toBeChecked();
      expect(within(dialog).getByLabelText('Nota (opcional)')).toHaveValue('');
      expect(within(dialog).getByRole('button', { name: 'Bloquear conta' })).toBeDisabled();
    });

    it('lifts the block with a DELETE and refreshes the card', async () => {
      const deleted: string[] = [];
      serveLookup(blocked());
      server.use(
        http.delete(`${API}/admin/trainers/:trainerId/access-block`, ({ params }) => {
          deleted.push(String(params['trainerId']));
          return HttpResponse.json(trainer());
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Levantar bloqueio' }));

      const dialog = await screen.findByRole('alertdialog');
      expect(
        within(dialog).getByText(/O acesso volta já, para o personal trainer e para os clientes/)
      ).toBeVisible();
      await user.click(within(dialog).getByRole('button', { name: 'Levantar bloqueio' }));

      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(deleted).toEqual([TRAINER_ID]);
      const card = await screen.findByRole('article', { name: 'Ana Silva' });
      expect(within(card).queryByText('Bloqueado')).not.toBeInTheDocument();
      expect(within(card).getByRole('button', { name: 'Bloquear conta' })).toBeVisible();
      expect(toastMock.success).toHaveBeenCalledWith('Bloqueio levantado.');
    });

    it('closes the dialog and warns when the trainer no longer exists', async () => {
      serveLookup(trainer());
      server.use(
        http.post(`${API}/admin/trainers/:trainerId/access-block`, () =>
          HttpResponse.json(problem('admin_trainer_not_found'), { status: 404 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Bloquear conta' }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByLabelText('Conteúdo malicioso'));
      await user.click(within(dialog).getByRole('button', { name: 'Bloquear conta' }));

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(toastMock.error).toHaveBeenCalledWith('Este treinador já não existe.');
    });

    it('keeps the dialog open and explains a concurrency conflict', async () => {
      serveLookup(trainer());
      server.use(
        http.post(`${API}/admin/trainers/:trainerId/access-block`, () =>
          HttpResponse.json(problem('admin_trainer_concurrency_conflict'), { status: 409 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Bloquear conta' }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByLabelText('Conteúdo malicioso'));
      await user.click(within(dialog).getByRole('button', { name: 'Bloquear conta' }));

      await waitFor(() =>
        expect(toastMock.error).toHaveBeenCalledWith(
          'A conta foi alterada entretanto. Tenta novamente.'
        )
      );
      expect(screen.getByRole('dialog')).toBeVisible();
      expect(within(screen.getByRole('dialog')).getByLabelText('Conteúdo malicioso')).toBeChecked();
    });

    it('reads the real code of a 400 from errors[0] for the note length', async () => {
      serveLookup(trainer());
      server.use(
        http.post(`${API}/admin/trainers/:trainerId/access-block`, () =>
          HttpResponse.json(
            problem('validation_failed', {
              errors: [{ field: 'note', code: 'platform_enforcement_note_too_long', message: 'x' }],
            }),
            { status: 400 }
          )
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Bloquear conta' }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByLabelText('Conteúdo malicioso'));
      await user.click(within(dialog).getByRole('button', { name: 'Bloquear conta' }));

      await waitFor(() =>
        expect(toastMock.error).toHaveBeenCalledWith('A nota não pode ter mais de 500 caracteres.')
      );
      expect(screen.getByRole('dialog')).toBeVisible();
    });

    it('explains an invalid reason from the server', async () => {
      serveLookup(trainer());
      server.use(
        http.post(`${API}/admin/trainers/:trainerId/access-block`, () =>
          HttpResponse.json(
            problem('validation_failed', {
              errors: [
                { field: 'reason_code', code: 'platform_enforcement_reason_invalid', message: 'x' },
              ],
            }),
            { status: 400 }
          )
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Bloquear conta' }));
      const dialog = await screen.findByRole('dialog');
      await user.click(within(dialog).getByLabelText('Conteúdo malicioso'));
      await user.click(within(dialog).getByRole('button', { name: 'Bloquear conta' }));

      await waitFor(() =>
        expect(toastMock.error).toHaveBeenCalledWith('Escolhe um dos motivos da lista.')
      );
    });

    it('shows the generic message on a 403 and keeps the lift dialog open', async () => {
      serveLookup(blocked());
      server.use(
        http.delete(`${API}/admin/trainers/:trainerId/access-block`, () =>
          HttpResponse.json(problem('trainer_administration_administrator_only'), { status: 403 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await search(user);
      await user.click(await screen.findByRole('button', { name: 'Levantar bloqueio' }));
      await user.click(
        within(await screen.findByRole('alertdialog')).getByRole('button', {
          name: 'Levantar bloqueio',
        })
      );

      await waitFor(() =>
        expect(toastMock.error).toHaveBeenCalledWith(
          'Não foi possível concluir a ação. Tenta novamente.'
        )
      );
      expect(screen.getByRole('alertdialog')).toBeVisible();
    });
  });

  describe('exempt trainers list', () => {
    it('lists 25 per page with name, email, exemption date and the remove action', async () => {
      const lists = serveLists({ exempt: [exemptTrainer()], total: 60 });
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      const row = (await screen.findByRole('rowheader', { name: 'Ana Silva' })).closest('tr');
      expect(row).not.toBeNull();
      const cells = within(row as HTMLElement);
      expect(cells.getByText(EMAIL)).toBeVisible();
      expect(cells.getByText(/14\/09\/2026.*10:00/)).toBeVisible();
      expect(cells.getByRole('button', { name: 'Tirar isenção a Ana Silva' })).toBeVisible();
      expect(lists.exemptQueries[0]?.get('page_size')).toBe('25');
      expect(lists.exemptQueries[0]?.get('page_number')).toBe('1');

      await user.click(screen.getByRole('button', { name: 'Seguinte' }));

      await waitFor(() => expect(lists.exemptQueries.at(-1)?.get('page_number')).toBe('2'));
      expect(screen.getByText('Página 2 de 3')).toBeVisible();
    });

    it('removes the exemption from a row through the same confirmation', async () => {
      const lists = serveLists({ exempt: [exemptTrainer()] });
      const deleted: string[] = [];
      server.use(
        http.delete(`${API}/admin/trainers/:trainerId/billing-exemption`, ({ params }) => {
          deleted.push(String(params['trainerId']));
          lists.state.exempt = [];
          return HttpResponse.json(
            trainer({ subscription_tier: 'FREE', subscription_status: 'INACTIVE' })
          );
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await user.click(await screen.findByRole('button', { name: 'Tirar isenção a Ana Silva' }));
      const dialog = await screen.findByRole('alertdialog');
      expect(within(dialog).getByText(/Passa ao plano Free, inativo\./)).toBeVisible();
      await user.click(within(dialog).getByRole('button', { name: 'Tirar isenção' }));

      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(deleted).toEqual([TRAINER_ID]);
      expect(await screen.findByText('Ainda não há treinadores isentos.')).toBeVisible();
    });
  });

  describe('pending invitations list', () => {
    it('shows the valid date for a live invitation and «Expirou» for an expired one', async () => {
      serveLists({
        invitations: [
          invitation(),
          invitation({
            invitation_id: '77777777-7777-7777-7777-777777777777',
            email: 'velho@ptmanager.test',
            expires_at: '2020-01-10T09:00:00Z',
          }),
        ],
      });
      renderApp({ initialEntries: ['/admin/trainers'] });

      const live = (await screen.findByRole('rowheader', { name: 'novo@ptmanager.test' })).closest(
        'tr'
      ) as HTMLElement;
      const old = screen
        .getByRole('rowheader', { name: 'velho@ptmanager.test' })
        .closest('tr') as HTMLElement;

      expect(within(live).getByText(/10\/01\/2099.*09:00/)).toBeVisible();
      expect(within(live).queryByText('Expirou')).not.toBeInTheDocument();
      expect(within(old).getByText('Expirou')).toBeVisible();
      expect(within(old).getByRole('button', { name: /Enviar outro/ })).toBeVisible();
    });

    it('resends an invitation from the list and refreshes the list', async () => {
      const lists = serveLists({ invitations: [invitation()] });
      const bodies: unknown[] = [];
      server.use(
        http.post(`${API}/admin/trainer-invitations`, async ({ request }) => {
          bodies.push(await request.json());
          lists.state.invitations = [
            invitation({
              invitation_id: '88888888-8888-8888-8888-888888888888',
              expires_at: '2099-03-01T09:00:00Z',
            }),
          ];
          return HttpResponse.json(lists.state.invitations[0], { status: 201 });
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await user.click(
        await screen.findByRole('button', { name: 'Enviar outro a novo@ptmanager.test' })
      );

      await waitFor(() =>
        expect(toastMock.success).toHaveBeenCalledWith(
          'Convite reenviado. O link anterior deixou de servir.'
        )
      );
      expect(bodies).toEqual([{ email: 'novo@ptmanager.test' }]);
      expect(await screen.findByText(/01\/03\/2099.*09:00/)).toBeVisible();
    });

    it('shows the email problem as a toast when resending fails', async () => {
      serveLists({ invitations: [invitation()] });
      server.use(
        http.post(`${API}/admin/trainer-invitations`, () =>
          HttpResponse.json(problem('authentication_email_delivery_unavailable'), { status: 503 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await user.click(
        await screen.findByRole('button', { name: 'Enviar outro a novo@ptmanager.test' })
      );

      await waitFor(() =>
        expect(toastMock.error).toHaveBeenCalledWith(
          'Não foi possível enviar o email. Tenta novamente.'
        )
      );
    });

    it('refreshes the pending list even when the resend email fails', async () => {
      let listRequests = 0;
      serveLists({ invitations: [invitation()] });
      server.use(
        http.get(`${API}/admin/trainer-invitations`, () => {
          listRequests += 1;
          return HttpResponse.json([invitation()]);
        }),
        http.post(`${API}/admin/trainer-invitations`, () =>
          HttpResponse.json(problem('authentication_email_delivery_unavailable'), { status: 503 })
        )
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await user.click(
        await screen.findByRole('button', { name: 'Enviar outro a novo@ptmanager.test' })
      );

      await waitFor(() => expect(toastMock.error).toHaveBeenCalled());
      // Um pedido ao abrir e outro depois da falha: o convite já existe no servidor.
      await waitFor(() => expect(listRequests).toBeGreaterThanOrEqual(2));
    });

    it('revokes an invitation after confirming and drops it from the list', async () => {
      const lists = serveLists({ invitations: [invitation()] });
      const deleted: string[] = [];
      server.use(
        http.delete(`${API}/admin/trainer-invitations/:id`, ({ params }) => {
          deleted.push(String(params['id']));
          lists.state.invitations = [];
          return new HttpResponse(null, { status: 204 });
        })
      );
      const user = userEvent.setup();
      renderApp({ initialEntries: ['/admin/trainers'] });

      await user.click(
        await screen.findByRole('button', { name: 'Anular convite a novo@ptmanager.test' })
      );
      const dialog = await screen.findByRole('alertdialog');
      await user.click(within(dialog).getByRole('button', { name: 'Anular convite' }));

      await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
      expect(deleted).toEqual(['55555555-5555-5555-5555-555555555555']);
      expect(await screen.findByText('Não há convites pendentes.')).toBeVisible();
    });
  });
});
