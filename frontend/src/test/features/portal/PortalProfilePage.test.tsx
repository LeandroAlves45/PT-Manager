import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { portalKeys } from '@/features/portal';
import { API, problem, restorableSession } from '@/test/msw/handlers';
import { portalProfile } from '@/test/msw/portal-fixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const PROFILE = `${API}/portal/my-profile`;
const AVATAR = `${API}/portal/my-profile/avatar`;
const PHOTO = 'https://res.cloudinary.com/demo/image/upload/v1/pt-manager/avatar-1.webp';

/**
 * O `FormData` e o `File` do jsdom não passam pelo `Request` do Node (undici), que o
 * `openapi-fetch` usa nos testes. Os testes de upload usam as classes do Node, como no logo
 * do personal trainer (`SettingsPage.test.tsx`).
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

async function openProfile(profile = portalProfile()) {
  server.use(
    ...restorableSession({ role: 'client' }),
    http.get(PROFILE, () => HttpResponse.json(profile))
  );
  const view = renderApp({ initialEntries: ['/portal/profile'] });
  await screen.findByRole('heading', { name: 'Perfil' });
  return view;
}

const section = (name: string) => screen.getByRole('region', { name });
const field = (name: string) => screen.getByRole('textbox', { name });
const fileInput = () => screen.getByLabelText('Ficheiro da fotografia');

describe('PortalProfilePage', () => {
  beforeEach(() => {
    toastMock.success.mockClear();
    toastMock.error.mockClear();
  });

  it('shows the profile from a single request shared with the header menu', async () => {
    let requests = 0;
    server.use(
      ...restorableSession({ role: 'client' }),
      http.get(PROFILE, () => {
        requests += 1;
        return HttpResponse.json(portalProfile());
      })
    );
    renderApp({ initialEntries: ['/portal/profile'] });
    await screen.findByRole('heading', { name: 'Perfil' });

    const record = within(section('Os teus dados'));
    expect(record.getByText('João Pereira')).toBeInTheDocument();
    expect(record.getByText('17/04/1992')).toBeInTheDocument();
    expect(record.getByText('Masculino')).toBeInTheDocument();
    expect(
      record.getByText('Para alterar estes dados, fala com o teu personal trainer.')
    ).toBeInTheDocument();
    // Nome, nascimento e sexo não são campos: o backend ignora-os no pedido.
    expect(record.queryByRole('textbox')).not.toBeInTheDocument();

    expect(field('Telefone')).toHaveValue('+351912345678');
    expect(field('Email de contacto (opcional)')).toHaveValue('joao@ptmanager.local');
    expect(field('Contacto de emergência: nome (opcional)')).toHaveValue('');
    expect(field('Contacto de emergência: telefone (opcional)')).toHaveValue('');

    const photo = within(section('Fotografia'));
    expect(photo.getByTestId('client-avatar-initials')).toHaveTextContent('JP');
    expect(photo.getByRole('button', { name: 'Enviar fotografia' })).toBeInTheDocument();
    expect(photo.queryByRole('button', { name: 'Remover' })).not.toBeInTheDocument();
    // O menu do topo e a página leem a mesma cache.
    expect(requests).toBe(1);
  });

  it('saves the four contacts, sending an emptied optional as null', async () => {
    let body: unknown = null;
    server.use(
      http.patch(PROFILE, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(
          portalProfile({
            contact_email: null,
            phone: '961112223',
            emergency_contact_name: 'Ana Pereira',
            emergency_contact_phone: '912000111',
            updated_at: '2026-10-04T11:00:00Z',
          })
        );
      })
    );
    const user = userEvent.setup();
    const { queryClient } = await openProfile();

    await user.clear(field('Telefone'));
    await user.type(field('Telefone'), ' 961112223 ');
    await user.clear(field('Email de contacto (opcional)'));
    await user.type(field('Contacto de emergência: nome (opcional)'), 'Ana Pereira');
    await user.type(field('Contacto de emergência: telefone (opcional)'), '912000111');
    await user.click(screen.getByRole('button', { name: 'Guardar contactos' }));

    await waitFor(() =>
      expect(toastMock.success).toHaveBeenCalledWith('Contactos guardados com sucesso.')
    );
    expect(body).toEqual({
      contact_email: null,
      phone: '961112223',
      emergency_contact_name: 'Ana Pereira',
      emergency_contact_phone: '912000111',
    });
    // A resposta substitui a cache, sem um GET a seguir.
    expect(queryClient.getQueryData(portalKeys.profile())).toMatchObject({ phone: '961112223' });
    expect(field('Telefone')).toHaveValue('961112223');
  });

  it('refuses locally an empty phone and an email the backend would reject', async () => {
    let patches = 0;
    server.use(
      http.patch(PROFILE, () => {
        patches += 1;
        return HttpResponse.json(portalProfile());
      })
    );
    const user = userEvent.setup();
    await openProfile();

    await user.clear(field('Telefone'));
    await user.clear(field('Email de contacto (opcional)'));
    await user.type(field('Email de contacto (opcional)'), 'a@b');
    await user.click(screen.getByRole('button', { name: 'Guardar contactos' }));

    expect(await screen.findByText('Indica o telefone.')).toBeInTheDocument();
    expect(screen.getByText('Indica um email válido.')).toBeInTheDocument();
    expect(field('Telefone')).toHaveAttribute('aria-invalid', 'true');
    expect(patches).toBe(0);
  });

  it('shows a server refusal on its field and lets the client save again', async () => {
    const bodies: unknown[] = [];
    server.use(
      http.patch(PROFILE, async ({ request }) => {
        bodies.push(await request.json());
        if (bodies.length === 1)
          return HttpResponse.json(problem('client_phone_already_exists'), { status: 409 });
        return HttpResponse.json(portalProfile({ phone: '961112224' }));
      })
    );
    const user = userEvent.setup();
    await openProfile();

    await user.clear(field('Telefone'));
    await user.type(field('Telefone'), '961112223');
    await user.click(screen.getByRole('button', { name: 'Guardar contactos' }));

    expect(
      await screen.findByText('Este telefone já está a ser usado noutra ficha de cliente.')
    ).toBeInTheDocument();
    expect(field('Telefone')).toHaveAttribute('aria-invalid', 'true');
    expect(field('Telefone')).toHaveValue('961112223');
    expect(toastMock.success).not.toHaveBeenCalled();

    await user.clear(field('Telefone'));
    await user.type(field('Telefone'), '961112224');
    await user.click(screen.getByRole('button', { name: 'Guardar contactos' }));

    await waitFor(() => expect(toastMock.success).toHaveBeenCalledTimes(1));
    expect(bodies).toHaveLength(2);
  });

  it('maps a validation error from errors[] to its field', async () => {
    server.use(
      http.patch(PROFILE, () =>
        HttpResponse.json(
          {
            ...problem('validation_failed'),
            errors: [{ field: 'ContactEmail', code: 'client_email_invalid', message: 'invalid' }],
          },
          { status: 400 }
        )
      )
    );
    const user = userEvent.setup();
    await openProfile();

    await user.click(screen.getByRole('button', { name: 'Guardar contactos' }));

    expect(await screen.findByText('Indica um email válido.')).toBeInTheDocument();
    expect(field('Email de contacto (opcional)')).toHaveAttribute('aria-invalid', 'true');
  });

  it('refuses a file of the wrong type without sending it', async () => {
    let uploads = 0;
    server.use(
      http.put(AVATAR, () => {
        uploads += 1;
        return HttpResponse.json(portalProfile());
      })
    );
    const user = userEvent.setup({ applyAccept: false });
    await openProfile();

    await user.upload(fileInput(), new File(['gif'], 'foto.gif', { type: 'image/gif' }));

    expect(await screen.findByText('Escolhe uma imagem PNG, JPEG ou WebP.')).toBeInTheDocument();
    expect(fileInput()).toHaveAttribute('aria-invalid', 'true');
    expect(uploads).toBe(0);
  });

  it('uploads the photo as multipart and shows it in the page and in the header', async () => {
    let body = '';
    server.use(
      http.put(AVATAR, async ({ request }) => {
        expect(request.headers.get('content-type')).toMatch(/^multipart\/form-data; boundary=/);
        body = await request.text();
        return HttpResponse.json(portalProfile({ avatar_url: PHOTO }));
      })
    );
    const user = userEvent.setup();
    await openProfile();

    await user.upload(fileInput(), await nodeFile('foto.png', 'image/png'));

    await waitFor(() =>
      expect(toastMock.success).toHaveBeenCalledWith('Fotografia atualizada com sucesso.')
    );
    expect(body).toContain('name="file"; filename="foto.png"');
    expect(body).toContain('Content-Type: image/png');
    const photo = within(section('Fotografia'));
    expect(photo.getByTestId('client-avatar-image')).toHaveAttribute('src', PHOTO);
    expect(photo.getByRole('button', { name: 'Substituir fotografia' })).toBeInTheDocument();
    expect(photo.getByRole('button', { name: 'Remover' })).toBeInTheDocument();
    // O menu do topo acompanha, sem outro pedido.
    expect(
      within(screen.getByRole('button', { name: 'Menu de perfil' })).getByTestId(
        'client-avatar-image'
      )
    ).toHaveAttribute('src', PHOTO);
  });

  it.each([
    [
      'the moderation refusal',
      400,
      {
        ...problem('validation_failed'),
        errors: [{ field: 'Avatar', code: 'portal_avatar_content_not_allowed', message: 'x' }],
      },
      'Esta imagem não foi aceite. Escolhe outra fotografia.',
    ],
    [
      'moderation being down',
      503,
      problem('portal_avatar_moderation_unavailable'),
      'Não é possível enviar fotografias neste momento. Nada foi alterado. Tenta mais tarde.',
    ],
    [
      'the hourly upload limit',
      429,
      { status: 429, title: 'Too many requests', detail: 'Rate limit exceeded.' },
      'Enviaste demasiadas fotografias. Tenta de novo dentro de 1 hora.',
    ],
  ])('explains %s and keeps the current photo', async (_case, status, responseBody, message) => {
    server.use(http.put(AVATAR, () => HttpResponse.json(responseBody, { status })));
    const user = userEvent.setup();
    await openProfile();

    await user.upload(fileInput(), await nodeFile('foto.png', 'image/png'));

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(within(section('Fotografia')).getByRole('alert')).toHaveTextContent(message);
    expect(within(section('Fotografia')).getByTestId('client-avatar-initials')).toHaveTextContent(
      'JP'
    );
    expect(toastMock.success).not.toHaveBeenCalled();
    // O botão volta a ficar disponível para escolher outra imagem.
    expect(screen.getByRole('button', { name: 'Enviar fotografia' })).toBeEnabled();
  });

  it('removes the photo after confirmation and closes the dialog', async () => {
    let deletes = 0;
    server.use(
      http.delete(AVATAR, () => {
        deletes += 1;
        return HttpResponse.json(portalProfile({ avatar_url: null }));
      })
    );
    const user = userEvent.setup();
    await openProfile(portalProfile({ avatar_url: PHOTO }));

    await user.click(within(section('Fotografia')).getByRole('button', { name: 'Remover' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'Remover a fotografia?' });
    expect(deletes).toBe(0);
    await user.click(within(dialog).getByRole('button', { name: 'Remover fotografia' }));

    await waitFor(() =>
      expect(toastMock.success).toHaveBeenCalledWith('Fotografia removida com sucesso.')
    );
    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(deletes).toBe(1);
    expect(within(section('Fotografia')).getByTestId('client-avatar-initials')).toHaveTextContent(
      'JP'
    );
    expect(
      within(section('Fotografia')).queryByRole('button', { name: 'Remover' })
    ).not.toBeInTheDocument();
  });

  it('does not wipe the contacts being typed when the photo changes', async () => {
    server.use(
      http.put(AVATAR, () =>
        // O upload muda o perfil (fotografia e data de alteração).
        HttpResponse.json(portalProfile({ avatar_url: PHOTO, updated_at: '2026-10-04T12:00:00Z' }))
      )
    );
    const user = userEvent.setup();
    await openProfile();

    await user.type(field('Contacto de emergência: nome (opcional)'), 'Ana Pereira');
    await user.upload(fileInput(), await nodeFile('foto.png', 'image/png'));

    await waitFor(() => expect(toastMock.success).toHaveBeenCalled());
    expect(field('Contacto de emergência: nome (opcional)')).toHaveValue('Ana Pereira');
  });

  it('falls back to the initials when the photo fails to load', async () => {
    await openProfile(portalProfile({ avatar_url: PHOTO }));
    const image = within(section('Fotografia')).getByTestId('client-avatar-image');

    fireEvent.error(image);

    expect(
      await within(section('Fotografia')).findByTestId('client-avatar-initials')
    ).toHaveTextContent('JP');
  });

  it('treats an archived client as an unavailable portal, without retry', async () => {
    server.use(
      ...restorableSession({ role: 'client' }),
      http.get(PROFILE, () =>
        HttpResponse.json(problem('portal_profile_not_available'), { status: 404 })
      )
    );
    renderApp({ initialEntries: ['/portal/profile'] });

    expect(await screen.findByRole('heading', { name: 'Portal indisponível' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();
  });

  it('treats a technical failure as an error, with retry', async () => {
    let fail = true;
    server.use(
      ...restorableSession({ role: 'client' }),
      http.get(PROFILE, () =>
        fail
          ? HttpResponse.json(problem('Internal server error'), { status: 500 })
          : HttpResponse.json(portalProfile())
      )
    );
    const user = userEvent.setup();
    renderApp({ initialEntries: ['/portal/profile'] });

    const alert = await screen.findByRole('alert');
    fail = false;
    await user.click(within(alert).getByRole('button', { name: 'Tentar novamente' }));

    expect(await screen.findByRole('heading', { name: 'Perfil' })).toBeInTheDocument();
  });

  it('keeps the profile and warns when a refresh fails', async () => {
    const { queryClient } = await openProfile();

    server.use(
      http.get(PROFILE, () => HttpResponse.json(problem('Internal server error'), { status: 500 }))
    );
    await queryClient.invalidateQueries({ queryKey: portalKeys.profile() });

    expect(
      await screen.findByText(
        'Não foi possível atualizar. Estás a ver os últimos dados carregados.'
      )
    ).toBeInTheDocument();
    expect(field('Telefone')).toHaveValue('+351912345678');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('sends a trainer away from the client profile', async () => {
    let requests = 0;
    server.use(
      ...restorableSession({ role: 'trainer' }),
      http.get(PROFILE, () => {
        requests += 1;
        return HttpResponse.json(portalProfile());
      })
    );
    const { router } = renderApp({ initialEntries: ['/portal/profile'] });

    await waitFor(() => expect(router.state.location.pathname).toBe('/trainer'));
    await screen.findByRole('button', { name: 'Menu de perfil' });
    expect(screen.queryByRole('heading', { name: 'Perfil' })).not.toBeInTheDocument();
    // O menu de perfil é partilhado: o personal trainer nunca pede o perfil de cliente.
    expect(requests).toBe(0);
  });
});
