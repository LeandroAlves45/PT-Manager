import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { setViewport } from '@/test/browser-fakes';
import { trainerSettings } from '@/test/msw/account-fixtures';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { createTestQueryClient, renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const ROUTE = '/trainer/settings';

/**
 * O `FormData` e o `File` do jsdom não passam pelo `Request` do Node (undici), que o
 * `openapi-fetch` usa nos testes: o pedido rebenta antes de chegar ao MSW. Num browser
 * real são a mesma implementação. Os testes de upload usam as classes do Node: o `File` de
 * `node:buffer` (import dinâmico, o tsconfig da app não carrega os tipos do Node) e o
 * `FormData` obtido de uma `Response` (o jsdom substitui os globais, não o `Response`).
 */
async function nodeFile(name: string, type: string): Promise<File> {
  const { File: NodeFile } = (await import('node:' + 'buffer')) as { File: typeof File };
  const parsed = await new Response('', {
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
  }).formData();
  vi.stubGlobal('FormData', parsed.constructor);
  return new NodeFile(['image-bytes'], name, { type });
}

afterEach(() => vi.unstubAllGlobals());

/** `GET /trainer-settings` com as definições indicadas; devolve o contador de pedidos. */
function settingsHandler(settings = trainerSettings()) {
  let requests = 0;
  server.use(
    ...restorableSession(),
    http.get(`${API}/trainer-settings`, () => {
      requests += 1;
      return HttpResponse.json(settings);
    })
  );
  return () => requests;
}

function section(name: string) {
  return screen.getByRole('region', { name });
}

describe('SettingsPage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('saves the branding with null for empty colours and previews a valid colour', async () => {
    settingsHandler();
    let body: unknown = null;
    server.use(
      http.patch(`${API}/trainer-settings/branding`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(trainerSettings({ app_name: 'Marta Coach' }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    const name = await screen.findByLabelText('Nome da app');
    expect(name).toHaveValue('PT Marta');
    await user.clear(name);
    await user.type(name, '  Marta Coach ');
    await user.type(within(section('Marca')).getByLabelText('Cor de fundo'), '#112233');

    for (const theme of ['light', 'dark']) {
      expect(screen.getByTestId(`brand-preview-header-${theme}`)).toHaveStyle({
        backgroundColor: '#112233',
        color: '#ffffff',
      });
      expect(
        within(screen.getByTestId(`brand-preview-${theme}`)).getByText('Marta Coach')
      ).toBeInTheDocument();
    }

    await user.click(screen.getByRole('button', { name: 'Guardar marca' }));

    await waitFor(() =>
      expect(body).toEqual({ app_name: 'Marta Coach', primary_color: null, body_color: '#112233' })
    );
    expect(toastMock.success).toHaveBeenCalledWith('Marca guardada com sucesso.');
  });

  it('refuses an invalid colour and a short name without calling the API', async () => {
    settingsHandler();
    let patches = 0;
    server.use(
      http.patch(`${API}/trainer-settings/branding`, () => {
        patches += 1;
        return HttpResponse.json(trainerSettings());
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    const name = await screen.findByLabelText('Nome da app');
    await user.clear(name);
    await user.type(name, 'M');
    await user.type(within(section('Marca')).getByLabelText('Cor principal'), 'azul');
    await user.click(screen.getByRole('button', { name: 'Guardar marca' }));

    expect(
      await screen.findByText('O nome da app tem de ter entre 2 e 50 caracteres.')
    ).toBeInTheDocument();
    expect(
      screen.getByText('Usa uma cor no formato #RRGGBB ou deixa vazio para a cor do tema.')
    ).toBeInTheDocument();
    // Uma cor inválida nunca chega ao `style`; o botão fica com a cor PT
    // Manager de cada tema e o cabeçalho sem cor própria.
    const [light, dark] = screen.getAllByText('Treino de hoje');
    expect(light).toHaveStyle({ backgroundColor: '#0077b6', color: '#ffffff' });
    expect(dark).toHaveStyle({ backgroundColor: '#00a3e9', color: '#03131c' });
    expect(screen.getByTestId('brand-preview-header-light')).not.toHaveAttribute('style');
    expect(patches).toBe(0);
  });

  it('previews the brand colour adjusted to each theme, as the portal shows it', async () => {
    settingsHandler();
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.type(
      within(await screen.findByRole('region', { name: 'Marca' })).getByLabelText('Cor principal'),
      '#E8642A'
    );

    const [light, dark] = screen.getAllByText('Treino de hoje');
    expect(light).toHaveStyle({ backgroundColor: '#ba5022', color: '#ffffff' });
    expect(dark).toHaveStyle({ backgroundColor: '#e8642a', color: '#000000' });
  });

  it('keeps an unsaved draft when the settings are fetched again with a different name', async () => {
    let requests = 0;
    server.use(
      ...restorableSession(),
      http.get(`${API}/trainer-settings`, () => {
        requests += 1;
        // A segunda leitura traz o nome alterado noutro separador: o rascunho prevalece.
        return HttpResponse.json(
          requests === 1 ? trainerSettings() : trainerSettings({ app_name: 'Outro separador' })
        );
      })
    );
    const queryClient = createTestQueryClient();
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE], queryClient });

    const name = await screen.findByLabelText('Nome da app');
    await user.clear(name);
    await user.type(name, 'Rascunho');
    await queryClient.invalidateQueries({ queryKey: ['trainer-settings'] });

    await waitFor(() => expect(requests).toBe(2));
    await waitFor(() =>
      expect(queryClient.getQueryData(['trainer-settings'])).toMatchObject({
        app_name: 'Outro separador',
      })
    );
    expect(screen.getByLabelText('Nome da app')).toHaveValue('Rascunho');
  });

  it('resets the colours with the dedicated endpoint', async () => {
    settingsHandler(trainerSettings({ primary_color: '#FF0000', body_color: '#000000' }));
    let resets = 0;
    server.use(
      http.post(`${API}/trainer-settings/branding/reset-colors`, () => {
        resets += 1;
        return HttpResponse.json(trainerSettings());
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByLabelText('Cor principal')).toHaveValue('#FF0000');
    await user.click(screen.getByRole('button', { name: 'Repor cores do tema' }));

    await waitFor(() => expect(resets).toBe(1));
    await waitFor(() => expect(screen.getByLabelText('Cor principal')).toHaveValue(''));
    expect(screen.queryByRole('button', { name: 'Repor cores do tema' })).not.toBeInTheDocument();
  });

  it('rejects a logo of the wrong type locally', async () => {
    settingsHandler();
    let uploads = 0;
    server.use(
      http.put(`${API}/trainer-settings/logo`, () => {
        uploads += 1;
        return HttpResponse.json(trainerSettings());
      })
    );
    const user = userEvent.setup({ applyAccept: false });
    renderApp({ initialEntries: [ROUTE] });

    await user.upload(
      await screen.findByLabelText('Ficheiro do logo'),
      new File(['gif'], 'logo.gif', { type: 'image/gif' })
    );

    expect(await screen.findByText('Escolhe uma imagem PNG, JPEG ou WebP.')).toBeInTheDocument();
    expect(uploads).toBe(0);
  });

  it('uploads the logo as multipart with the file part and shows the new logo', async () => {
    settingsHandler();
    let body = '';
    server.use(
      http.put(`${API}/trainer-settings/logo`, async ({ request }) => {
        expect(request.headers.get('content-type')).toMatch(/^multipart\/form-data; boundary=/);
        // Texto em vez de `formData()`: o parser do undici criaria um `File` do jsdom.
        body = await request.text();
        return HttpResponse.json(
          trainerSettings({ logo_url: 'https://res.cloudinary.com/demo/logo.webp' })
        );
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.upload(
      await screen.findByLabelText('Ficheiro do logo'),
      await nodeFile('logo.png', 'image/png')
    );

    expect(await screen.findByRole('img', { name: 'Logo de PT Marta' })).toHaveAttribute(
      'src',
      'https://res.cloudinary.com/demo/logo.webp'
    );
    expect(body).toContain('name="file"; filename="logo.png"');
    expect(body).toContain('Content-Type: image/png');
    expect(body).toContain('image-bytes');
    expect(toastMock.success).toHaveBeenCalledWith('Logo atualizado com sucesso.');
  });

  it('explains a server refusal of the image', async () => {
    settingsHandler();
    server.use(
      http.put(`${API}/trainer-settings/logo`, () =>
        HttpResponse.json(
          {
            ...problem('validation_failed'),
            errors: [
              {
                field: 'Logo',
                code: 'trainer_settings_logo_dimensions_too_small',
                message: 'too small',
              },
            ],
          },
          { status: 400 }
        )
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.upload(
      await screen.findByLabelText('Ficheiro do logo'),
      await nodeFile('logo.png', 'image/png')
    );

    expect(
      await screen.findByText('O logo tem de ter pelo menos 64 px de lado.')
    ).toBeInTheDocument();
  });

  it('removes the logo after confirmation', async () => {
    settingsHandler(trainerSettings({ logo_url: 'https://res.cloudinary.com/demo/logo.webp' }));
    let deletes = 0;
    server.use(
      http.delete(`${API}/trainer-settings/logo`, () => {
        deletes += 1;
        return HttpResponse.json(trainerSettings());
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.click(await screen.findByRole('button', { name: 'Remover' }));
    const dialog = await screen.findByRole('alertdialog');
    await user.click(within(dialog).getByRole('button', { name: 'Remover logo' }));

    await waitFor(() => expect(deletes).toBe(1));
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(toastMock.success).toHaveBeenCalledWith('Logo removido com sucesso.');
    expect(await screen.findByRole('button', { name: 'Enviar logo' })).toBeInTheDocument();
  });

  it('saves contacts with null for empty fields and maps a server field error', async () => {
    settingsHandler(trainerSettings({ city: 'Porto' }));
    const bodies: unknown[] = [];
    server.use(
      http.patch(`${API}/trainer-settings/contacts`, async ({ request }) => {
        bodies.push(await request.json());
        return bodies.length === 1
          ? HttpResponse.json(
              {
                ...problem('validation_failed'),
                errors: [
                  { field: 'Phone', code: 'trainer_settings_phone_too_long', message: 'long' },
                ],
              },
              { status: 400 }
            )
          : HttpResponse.json(trainerSettings({ phone: '912345678', city: null }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    await user.type(await screen.findByLabelText('Telefone'), '912345678');
    await user.clear(screen.getByLabelText('Cidade'));
    await user.click(screen.getByRole('button', { name: 'Guardar contactos' }));

    const phone = screen.getByLabelText('Telefone');
    await waitFor(() => expect(phone).toHaveAttribute('aria-invalid', 'true'));
    expect(screen.getByText('O telefone não pode exceder 20 caracteres.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Guardar contactos' }));
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[1]).toEqual({ phone: '912345678', address: null, city: null });
    expect(toastMock.success).toHaveBeenCalledWith('Contactos guardados com sucesso.');
  });

  it('explains a timezone conflict and refreshes date-based data after a change', async () => {
    setViewport(1440);
    settingsHandler();
    let subscriptionRequests = 0;
    const bodies: unknown[] = [];
    server.use(
      http.get(`${API}/billing/subscription`, () => {
        subscriptionRequests += 1;
        return HttpResponse.json({
          tier: 'STARTER',
          status: 'ACTIVE',
          client_limit: 25,
          current_client_count: 3,
          trial_ends_at: null,
        });
      }),
      http.patch(`${API}/trainer-settings/timezone`, async ({ request }) => {
        bodies.push(await request.json());
        return bodies.length === 1
          ? HttpResponse.json(problem('trainer_settings_schedule_conflict'), { status: 409 })
          : HttpResponse.json(trainerSettings({ timezone: 'America/New_York' }));
      })
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: [ROUTE] });

    const select = await screen.findByLabelText('Fuso');
    expect(select).toHaveValue('Europe/Lisbon');
    expect(screen.getByRole('button', { name: 'Guardar fuso' })).toBeDisabled();
    await user.selectOptions(select, 'America/New_York');
    await user.click(screen.getByRole('button', { name: 'Guardar fuso' }));

    expect(
      await screen.findByText(/um cliente ficaria com duas sessões agendadas no mesmo dia/)
    ).toBeInTheDocument();

    await waitFor(() => expect(subscriptionRequests).toBe(1));
    await user.click(screen.getByRole('button', { name: 'Guardar fuso' }));

    await waitFor(() => expect(bodies[1]).toEqual({ timezone: 'America/New_York' }));
    await waitFor(() => expect(subscriptionRequests).toBe(2));
    expect(toastMock.success).toHaveBeenCalledWith('Fuso horário guardado com sucesso.');
  });

  it('shows an error state with retry when the settings fail to load', async () => {
    server.use(
      ...restorableSession(),
      http.get(`${API}/trainer-settings`, () =>
        HttpResponse.json(problem('internal_error'), { status: 500 })
      )
    );
    renderApp({ initialEntries: [ROUTE] });

    expect(await screen.findByRole('button', { name: /Tentar novamente/ })).toBeInTheDocument();
  });
});
