import { expect, test } from '@playwright/test';

import { readEmailLink } from './support/emailLinks';
import { loginAs } from './support/session';

const PASSWORD = 'E2e-Password-1!';

/**
 * Registo de um personal trainer, convite de um cliente e primeiro acesso desse cliente.
 *
 * Um único teste longo, de propósito: o registo está limitado a 3 por hora por IP e cada
 * carregamento da página gasta pedidos do limitador de CSRF (10 por minuto).
 */
test('o trainer regista-se, convida um cliente e o cliente entra no portal', async ({
  page,
  browser,
}) => {
  const stamp = Date.now();
  const trainerEmail = `trainer-${stamp}@ptmanager.test`;
  const clientEmail = `cliente-${stamp}@ptmanager.test`;

  // 1. Registo: a conta só fica ativa depois de confirmar o email.
  await page.goto('/criar-conta');
  await page.getByLabel('Nome completo').fill('Trainer E2E');
  await page.getByLabel('Email').fill(trainerEmail);
  await page.getByLabel('Palavra-passe', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Criar conta' }).click();
  await expect(page.getByRole('heading', { name: 'Verifica o teu email' })).toBeVisible();

  // 2. Confirmação do email pelo link que o backend escreveu no log.
  await page.goto(await readEmailLink('email_confirmation', trainerEmail));
  await expect(page).toHaveURL(/\/entrar/);
  await expect(page.getByText('Email confirmado. Já podes entrar.')).toBeVisible();

  // 3. Primeiro login do trainer.
  await page.getByLabel('Email').fill(trainerEmail);
  await page.getByLabel('Palavra-passe', { exact: true }).fill(PASSWORD);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page).toHaveURL(/\/trainer/);

  // 4. Cria um cliente com email de contacto e convida-o para o portal.
  await page.getByRole('link', { name: 'Clientes' }).first().click();
  await page.getByRole('link', { name: 'Novo cliente' }).first().click();
  await page.getByLabel('Nome', { exact: true }).fill('Cliente E2E');
  await page.getByLabel('Email de contacto').fill(clientEmail);
  await page.getByLabel('Telefone', { exact: true }).fill('912345678');
  await page.getByLabel('Data de nascimento').fill('1990-05-17');
  await page.getByLabel('Sexo biológico').selectOption({ index: 1 });
  await page.getByRole('button', { name: 'Criar', exact: true }).click();
  await expect(page.getByText('Cliente criado com sucesso.')).toBeVisible();

  await page.getByRole('button', { name: 'Convidar para o portal' }).click();
  await page.getByRole('alertdialog').getByRole('button', { name: 'Enviar convite' }).click();
  await expect(page.getByText('Convite enviado', { exact: true }).first()).toBeVisible();

  // 5. O cliente, noutro contexto de browser, aceita o convite e fica com sessão no portal.
  const clientContext = await browser.newContext({ ignoreHTTPSErrors: true, locale: 'pt-PT' });
  const clientPage = await clientContext.newPage();
  await clientPage.goto(await readEmailLink('client_invitation', clientEmail));
  await expect(clientPage.getByRole('heading', { name: 'Aceitar convite' })).toBeVisible();
  await clientPage.getByLabel('Palavra-passe', { exact: true }).fill(PASSWORD);
  await clientPage.getByLabel('Confirmar palavra-passe', { exact: true }).fill(PASSWORD);
  await clientPage.getByRole('button', { name: 'Criar conta e aceitar' }).click();
  await expect(clientPage).toHaveURL(/\/portal/);
  await expect(clientPage.getByRole('heading', { name: 'Hoje', exact: true })).toBeVisible();

  // 6. Sem sessão, o cliente volta a entrar com a palavra-passe que acabou de criar.
  await clientContext.clearCookies();
  await loginAs(clientPage, clientEmail, PASSWORD, /\/portal/);
  await clientContext.close();
});
