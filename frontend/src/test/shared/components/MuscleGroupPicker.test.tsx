import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';

import { MuscleGroupPicker } from '@/shared/components/MuscleGroupPicker';
import type { MuscleGroupCode } from '@/shared/lib/muscleGroups';

/** Estado controlado como num formulário, com o valor visível para as asserções. */
function Harness({ initial = [] }: { initial?: MuscleGroupCode[] }) {
  const [value, setValue] = useState<MuscleGroupCode[]>(initial);
  return (
    <>
      <label htmlFor="muscle-groups">Grupos musculares</label>
      <MuscleGroupPicker id="muscle-groups" value={value} onChange={setValue} />
      <output aria-label="valor">{value.join(',')}</output>
    </>
  );
}

const value = () => screen.getByRole('status', { name: 'valor' });

describe('MuscleGroupPicker', () => {
  it('toggles groups in the list and keeps it open for more choices', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('combobox', { name: 'Grupos musculares' }));
    await user.click(await screen.findByRole('option', { name: 'Dorsais' }));
    await user.click(screen.getByRole('option', { name: 'Bíceps' }));
    await user.click(screen.getByRole('option', { name: /Dorsais/ }));

    expect(value()).toHaveTextContent(/^biceps$/);
    expect(screen.getByRole('option', { name: 'Bíceps, selecionado' })).toBeVisible();
  });

  it('filters the fixed list locally', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('combobox'));
    await user.type(await screen.findByPlaceholderText('Filtrar grupos…'), 'glú');

    expect(screen.getAllByRole('option').map((option) => option.textContent)).toEqual(['Glúteos']);
  });

  it('removes the last chosen group with Backspace on an empty filter', async () => {
    const user = userEvent.setup();
    render(<Harness initial={['chest', 'triceps']} />);

    await user.click(screen.getByRole('combobox', { name: 'Grupos musculares' }));
    await user.type(await screen.findByPlaceholderText('Filtrar grupos…'), 'x{Backspace}');
    expect(value()).toHaveTextContent(/^chest,triceps$/);
    await user.keyboard('{Backspace}');

    expect(value()).toHaveTextContent(/^chest$/);
  });

  it('removes a group from its chip', async () => {
    const user = userEvent.setup();
    render(<Harness initial={['chest', 'triceps']} />);

    await user.click(screen.getByRole('button', { name: 'Remover Peito' }));

    expect(value()).toHaveTextContent(/^triceps$/);
    expect(screen.queryByRole('button', { name: 'Remover Peito' })).toBeNull();
  });
});
