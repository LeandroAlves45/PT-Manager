import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { PlatformEnforcementBadge } from '@/shared/components/PlatformEnforcementBadge';

describe('PlatformEnforcementBadge', () => {
  it('shows «Bloqueado», the reason label and the note in text', () => {
    render(
      <PlatformEnforcementBadge
        enforcement={{ reason: 'deliberately_false_information', note: 'Promete resultados' }}
      />
    );

    expect(screen.getByText('Bloqueado')).toBeInTheDocument();
    expect(
      screen.getByText('Motivo: Informação deliberadamente falsa · Nota: Promete resultados')
    ).toBeInTheDocument();
  });

  it('leaves the note out when there is none or it is only spaces', () => {
    const { rerender } = render(
      <PlatformEnforcementBadge enforcement={{ reason: 'malicious_content', note: null }} />
    );
    expect(screen.getByText('Motivo: Conteúdo malicioso')).toBeInTheDocument();

    rerender(
      <PlatformEnforcementBadge enforcement={{ reason: 'malicious_content', note: '   ' }} />
    );
    expect(screen.queryByText(/Nota:/)).not.toBeInTheDocument();
  });

  it('shows only «Bloqueado» for a reason it does not know and no note', () => {
    const { container } = render(
      <PlatformEnforcementBadge enforcement={{ reason: 'outro', note: null }} className="pt-1" />
    );

    expect(screen.getByText('Bloqueado')).toBeInTheDocument();
    expect(screen.queryByText(/Motivo:/)).not.toBeInTheDocument();
    expect(container.firstElementChild).toHaveClass('pt-1');
  });
});
