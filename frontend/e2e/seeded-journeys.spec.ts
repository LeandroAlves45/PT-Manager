import { expect, test } from '@playwright/test';

import { loginAs, SEED, seedPassword, weekdayInLisbon } from './support/session';

/**
 * Fluxos com as contas semeadas (`DevelopmentSeed`): o trainer Leandro Alves e o cliente
 * João Pereira, com o plano "Hipertrofia - bloco 1" e uma avaliação periódica para hoje.
 */
test.describe.serial('contas semeadas', () => {
  test('o cliente responde à avaliação periódica de hoje', async ({ page }) => {
    await loginAs(page, SEED.client, seedPassword(), /\/portal/);

    await page.goto('/portal/check-ins');
    await expect(page.getByRole('heading', { name: 'Avaliação periódica' })).toBeVisible();

    await page.getByLabel('Peso (kg)').fill('80');
    await page.getByRole('button', { name: 'Seguinte' }).click();
    await page.getByRole('button', { name: 'Seguinte' }).click();
    await page.getByRole('button', { name: 'Enviar avaliação' }).click();

    await expect(
      page.getByText('Avaliação enviada. O teu personal trainer já a pode ver.')
    ).toBeVisible();
  });

  // O plano semeado tem treino à terça (Inferiores) e à quinta (Superiores).
  test('o cliente regista as séries e conclui o treino de hoje', async ({ page }) => {
    const weekday = weekdayInLisbon();
    test.skip(
      weekday !== 'Tue' && weekday !== 'Thu',
      'O plano semeado só tem treino à terça e à quinta.'
    );

    await loginAs(page, SEED.client, seedPassword(), /\/portal/);
    await page.getByRole('link', { name: 'Treino' }).first().click();
    await expect(page.getByRole('heading', { name: 'Treino de hoje' })).toBeVisible();

    // Cada clique troca o botão "Registar" por "Desmarcar", por isso o total desce de um em um.
    const pendingSets = page.getByRole('button', { name: /^Registar série \d+ de / });
    for (let remaining = await pendingSets.count(); remaining > 0; remaining--) {
      await pendingSets.first().click();
      await expect(pendingSets).toHaveCount(remaining - 1);
    }

    await page.getByRole('button', { name: 'Concluir treino' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Concluir treino' }).click();
    await expect(page.getByText('Treino concluído', { exact: false }).first()).toBeVisible();
  });

  test('o trainer agenda uma avaliação periódica', async ({ page }) => {
    await loginAs(page, SEED.trainer, seedPassword(), /\/trainer/);
    await page.goto('/trainer/check-ins/new');

    await page.getByLabel('Cliente').click();
    await page.getByPlaceholder('Pesquisar por nome').fill('João');
    await page.getByRole('option', { name: /João Pereira/ }).click();

    // Um dia futuro que varia por execução, para não colidir com avaliações anteriores.
    const offsetDays = 5 + (Math.floor(Date.now() / 1000) % 50);
    const day = new Date(Date.now() + offsetDays * 86_400_000).toISOString().slice(0, 10);
    await page.getByLabel('Dia da avaliação').fill(day);
    await page.getByRole('button', { name: 'Agendar' }).click();

    await expect(page.getByText('Avaliação periódica de João Pereira agendada.')).toBeVisible();
  });

  // Com o Stripe desligado o pedido tem de chegar à API (CORS com `Idempotency-Key`) e a
  // falha tratada aparece na página. O fluxo real em test mode é um gate manual (8C).
  test('o checkout sem Stripe mostra a indisponibilidade tratada', async ({ page }) => {
    await loginAs(page, SEED.trainer, seedPassword(), /\/trainer/);
    await page.getByRole('link', { name: 'Subscrição' }).first().click();
    await expect(page.getByRole('heading', { name: 'Subscrição' })).toBeVisible();

    await page.getByRole('button', { name: 'Escolher Starter' }).click();

    await expect(
      page
        .getByRole('alert')
        .filter({ hasText: 'Os pagamentos estão temporariamente indisponíveis.' })
    ).toBeVisible();
  });
});
