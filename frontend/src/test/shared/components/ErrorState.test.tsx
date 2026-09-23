import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { toApiProblem } from '@/shared/api/problem';
import { ErrorState } from '@/shared/components/ErrorState';
import { problem } from '@/test/msw/handlers';

const GENERIC = 'Não conseguimos completar a operação. Os dados continuam aqui. Tenta novamente.';

describe('ErrorState', () => {
  it('translates a known code to PT-PT instead of showing the backend detail', () => {
    render(<ErrorState error={toApiProblem(402, problem('subscription_required'))} />);

    expect(screen.getByText('Esta funcionalidade exige uma subscrição ativa.')).toBeInTheDocument();
    expect(screen.queryByText('detail for subscription_required')).not.toBeInTheDocument();
  });

  it('falls back to a short backend detail for an unknown code', () => {
    render(
      <ErrorState
        error={toApiProblem(409, problem('client_email_taken', { detail: 'Email já usado.' }))}
      />
    );

    expect(screen.getByText('Email já usado.')).toBeInTheDocument();
  });

  it.each([
    ['a non-API error', new Error('TypeError: x is undefined')],
    ['an overly long detail', toApiProblem(500, problem('boom', { detail: 'x'.repeat(241) }))],
  ])('shows the generic message for %s', (_label, error) => {
    render(<ErrorState error={error} />);

    expect(screen.getByText(GENERIC)).toBeInTheDocument();
  });

  it('offers retry only when there is something to retry', async () => {
    const onRetry = vi.fn();
    const { rerender } = render(<ErrorState error={new Error('x')} />);
    expect(screen.queryByRole('button', { name: 'Tentar novamente' })).not.toBeInTheDocument();

    rerender(<ErrorState error={new Error('x')} onRetry={onRetry} />);
    await userEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));

    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('copies the correlation id and survives a denied clipboard', async () => {
    const user = userEvent.setup();
    const writeText = vi
      .spyOn(navigator.clipboard, 'writeText')
      .mockRejectedValueOnce(new DOMException('denied', 'NotAllowedError'))
      .mockResolvedValueOnce();
    render(<ErrorState error={toApiProblem(500, problem('boom'))} />);

    await user.click(screen.getByRole('button', { name: 'Copiar id' }));
    expect(screen.getByRole('button', { name: 'Copiar id' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Copiar id' }));
    expect(await screen.findByRole('button', { name: 'Id copiado' })).toBeInTheDocument();
    expect(writeText).toHaveBeenLastCalledWith('corr-123');
  });
});
