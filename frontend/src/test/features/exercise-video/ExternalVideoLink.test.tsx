import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ExternalVideoLink } from '@/features/exercise-video';

describe('ExternalVideoLink', () => {
  it('names the site and says it opens in another tab', () => {
    render(<ExternalVideoLink url="https://vimeo.com/76979871" />);

    const link = screen.getByRole('link', { name: 'Ver no Vimeo (abre noutro separador)' });
    expect(link).toHaveAttribute('href', 'https://vimeo.com/76979871');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it.each([null, 'https://evil.example/watch', 'javascript:alert(1)'])(
    'draws nothing for %s',
    (url) => {
      const { container } = render(<ExternalVideoLink url={url} />);

      expect(container).toBeEmptyDOMElement();
    }
  );
});
