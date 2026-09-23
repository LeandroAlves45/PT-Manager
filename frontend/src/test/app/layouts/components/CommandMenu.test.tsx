import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

let searches: string[];

beforeEach(() => {
  searches = [];
  server.use(
    ...restorableSession({ role: 'trainer' }),
    http.get(`${API}/clients`, ({ request }) => {
      const search = new URL(request.url).searchParams.get('search') ?? '';
      searches.push(search);
      return HttpResponse.json({
        items: search === 'ana' ? [{ id: 'c-1', name: 'Ana Sousa' }] : [],
        page_number: 1,
        page_size: 5,
        total_count: search === 'ana' ? 1 : 0,
      });
    })
  );
});

async function openTrainerShell() {
  const user = userEvent.setup();
  renderApp({ initialEntries: ['/trainer'] });
  await screen.findByRole('heading', { name: 'Painel' });
  return user;
}

async function openMenu(user: ReturnType<typeof userEvent.setup>) {
  await user.keyboard('{Control>}k{/Control}');
  return screen.findByPlaceholderText('Pesquisar…');
}

describe('CommandMenu', () => {
  it('searches clients once open and lists the match', async () => {
    const user = await openTrainerShell();

    await user.type(await openMenu(user), 'ana');

    expect(await screen.findByRole('option', { name: 'Ana Sousa' })).toBeInTheDocument();
    expect(searches).toEqual(['ana']);
  });

  it.each([
    ['Escape', '{Escape}'],
    ['the shortcut', '{Control>}k{/Control}'],
  ])('clears the term and the results when closed with %s', async (_label, closeKeys) => {
    const user = await openTrainerShell();
    await user.type(await openMenu(user), 'ana');
    await screen.findByRole('option', { name: 'Ana Sousa' });

    await user.keyboard(closeKeys);
    await waitFor(() =>
      expect(screen.queryByPlaceholderText('Pesquisar…')).not.toBeInTheDocument()
    );

    expect(await openMenu(user)).toHaveValue('');
    expect(screen.queryByRole('option', { name: 'Ana Sousa' })).not.toBeInTheDocument();
    // Nem fechado nem ao reabrir se pesquisou outra vez.
    expect(searches).toEqual(['ana']);
  });
});
