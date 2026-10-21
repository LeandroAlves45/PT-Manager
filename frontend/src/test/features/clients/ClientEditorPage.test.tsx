import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { describe, expect, it, vi } from 'vitest';

import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { CLIENT_ID, clientDetails, clientSummaryOverview } from '@/test/msw/trainer-fixtures';
import { renderApp } from '@/test/render';

vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: { success: vi.fn(), error: vi.fn() },
}));

const DETAIL = `/trainer/clients/${CLIENT_ID}`;

/** Ficha, resumo e convite: o que a página de destino pede depois de guardar ou sair. */
function detailHandlers() {
  server.use(
    ...restorableSession(),
    http.get(`${API}/clients/:clientId`, () => HttpResponse.json(clientDetails())),
    http.get(`${API}/clients/:clientId/summary`, () => HttpResponse.json(clientSummaryOverview())),
    http.get(`${API}/clients/:clientId/invitation`, () =>
      HttpResponse.json({ state: 'none', email: null, expires_at: null })
    )
  );
}

describe('ClientEditorPage', () => {
  it('creates a client from its own page and opens the new record', async () => {
    let body: Record<string, unknown> | null = null;
    detailHandlers();
    server.use(
      http.post(`${API}/clients`, async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(clientDetails(), { status: 201 });
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: ['/trainer/clients/new'] });

    expect(await screen.findByRole('heading', { name: 'Criar cliente' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Voltar a Clientes/ })).toHaveAttribute(
      'href',
      '/trainer/clients'
    );
    await user.type(screen.getByLabelText('Nome'), 'Marta Figueiredo');
    await user.type(screen.getByLabelText('Email de contacto'), 'marta@example.com');
    await user.type(screen.getByLabelText('Telefone'), '+351912345680');
    await user.type(screen.getByLabelText('Data de nascimento'), '1992-04-17');
    await user.selectOptions(screen.getByLabelText('Sexo biológico'), 'female');
    await user.click(screen.getByRole('button', { name: 'Criar' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(DETAIL));
    expect(body).toMatchObject({
      name: 'Marta Figueiredo',
      contact_email: 'marta@example.com',
      phone: '+351912345680',
      birth_date: '1992-04-17',
      sex: 'female',
      objective: null,
    });
    // Guardar não pede confirmação de saída: o guard já está desligado quando navega.
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  }, 20000);

  it('loads the record on edit, sends the change and goes back to the record', async () => {
    let body: Record<string, unknown> | null = null;
    let method: string | null = null;
    detailHandlers();
    server.use(
      http.patch(`${API}/clients/:clientId`, async ({ request }) => {
        method = request.method;
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json(clientDetails({ phone: '+351911111111' }));
      })
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${DETAIL}/edit`] });

    expect(
      await screen.findByRole('heading', { name: 'Editar Marta Figueiredo' })
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Nome')).toHaveValue('Marta Figueiredo');
    expect(screen.getByLabelText('Email de contacto')).toHaveValue('marta@example.com');
    const phone = screen.getByLabelText('Telefone');
    await user.clear(phone);
    await user.type(phone, '+351911111111');
    await user.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(router.state.location.pathname).toBe(DETAIL));
    expect(method).toBe('PATCH');
    expect(body).toMatchObject({ name: 'Marta Figueiredo', phone: '+351911111111' });
  }, 20000);

  it('asks before leaving with unsaved changes and keeps them when the trainer stays', async () => {
    detailHandlers();
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${DETAIL}/edit`] });

    const name = await screen.findByLabelText('Nome');
    await user.type(name, ' Costa');
    await user.click(screen.getByRole('link', { name: /Voltar à ficha/ }));

    const dialog = await screen.findByRole('alertdialog');
    expect(within(dialog).getByText('Sair sem guardar?')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Continuar a editar' }));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(router.state.location.pathname).toBe(`${DETAIL}/edit`);
    expect(screen.getByLabelText('Nome')).toHaveValue('Marta Figueiredo Costa');

    await user.click(screen.getByRole('link', { name: /Voltar à ficha/ }));
    await user.click(
      within(await screen.findByRole('alertdialog')).getByRole('button', {
        name: 'Sair sem guardar',
      })
    );
    await waitFor(() => expect(router.state.location.pathname).toBe(DETAIL));
  }, 20000);

  it('leaves without asking when nothing changed', async () => {
    detailHandlers();
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${DETAIL}/edit`] });

    await screen.findByLabelText('Nome');
    await user.click(screen.getByRole('link', { name: /Voltar à ficha/ }));

    await waitFor(() => expect(router.state.location.pathname).toBe(DETAIL));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  }, 20000);

  it('reports a record that does not exist', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/clients/:clientId`, () =>
        HttpResponse.json(problem('client_not_found'), { status: 404 })
      )
    );
    renderApp({ initialEntries: [`${DETAIL}/edit`] });

    expect(await screen.findByText('Cliente não encontrado')).toBeInTheDocument();
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument();
  }, 15000);

  it('keeps the server error on the page when saving fails', async () => {
    detailHandlers();
    server.use(
      http.patch(`${API}/clients/:clientId`, () =>
        HttpResponse.json(problem('client_phone_already_exists'), { status: 409 })
      )
    );
    const user = userEvent.setup();
    const { router } = renderApp({ initialEntries: [`${DETAIL}/edit`] });

    await user.type(await screen.findByLabelText('Telefone'), '9');
    await user.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText('Já existe um cliente com este telefone.')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe(`${DETAIL}/edit`);
  }, 20000);
});
