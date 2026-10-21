import { render, screen } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { describe, expect, it } from 'vitest';

import { Breadcrumbs } from '@/shared/components/Breadcrumbs';
import { setViewport } from '@/test/browser-fakes';

function renderAt(entry: string) {
  setViewport(1440);
  const router = createMemoryRouter([{ path: '*', element: <Breadcrumbs /> }], {
    initialEntries: [entry],
  });
  render(<RouterProvider router={router} />);
}

describe('Breadcrumbs', () => {
  // Um editor aberto a partir da ficha não pode perder o cliente ao voltar um passo pela migalha.
  it('keeps the client of an editor opened from the client page', () => {
    renderAt('/trainer/training-plans/new/structure?client_id=c-1');

    expect(screen.getByRole('link', { name: 'Criar' })).toHaveAttribute(
      'href',
      '/trainer/training-plans/new?client_id=c-1'
    );
    // A lista e o painel não precisam do cliente.
    expect(screen.getByRole('link', { name: 'Planos de treino' })).toHaveAttribute(
      'href',
      '/trainer/training-plans'
    );
    expect(screen.getByText('Estrutura')).toHaveAttribute('aria-current', 'page');
  });

  it('builds plain links when there is no client', () => {
    renderAt('/trainer/meal-plans/new/meals');

    expect(screen.getByRole('link', { name: 'Criar' })).toHaveAttribute(
      'href',
      '/trainer/meal-plans/new'
    );
  });
});
