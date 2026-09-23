import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';

import { ThemeToggle } from '@/app/components/ThemeToggle';
import { THEME_STORAGE_KEY } from '@/app/providers/theme-context';
import { ThemeProvider } from '@/app/providers/ThemeProvider';

function renderToggle() {
  return render(
    <ThemeProvider>
      <ThemeToggle />
    </ThemeProvider>
  );
}

afterEach(() => {
  document.documentElement.className = '';
});

describe('ThemeToggle', () => {
  it('applies and remembers the chosen theme', async () => {
    const user = userEvent.setup();
    renderToggle();
    expect(document.documentElement).not.toHaveClass('dark');

    screen.getByRole('button', { name: 'Mudar tema' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Escuro' }));

    expect(document.documentElement).toHaveClass('dark');
    // eslint-disable-next-line no-restricted-globals -- preferência de tema, não é sessão
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('starts from the stored preference and ignores invalid values', () => {
    // eslint-disable-next-line no-restricted-globals -- preferência de tema, não é sessão
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');
    const { unmount } = renderToggle();
    expect(document.documentElement).toHaveClass('dark');
    unmount();

    // eslint-disable-next-line no-restricted-globals -- preferência de tema, não é sessão
    localStorage.setItem(THEME_STORAGE_KEY, 'neon');
    renderToggle();
    expect(document.documentElement).not.toHaveClass('dark');
  });
});
