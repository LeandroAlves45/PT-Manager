import { expect, type Page } from '@playwright/test';

/** Palavra-passe das contas semeadas; vem do mesmo valor que arranca a API. */
export function seedPassword(): string {
  const value = process.env.E2E_SEED_PASSWORD;
  if (!value) throw new Error('Define E2E_SEED_PASSWORD com o valor de DevelopmentSeed__Password.');
  return value;
}

export const SEED = {
  trainer: 'trainer@ptmanager.local',
  client: 'cliente@ptmanager.local',
} as const;

/** Entra pela página de login e espera pela área do papel. */
export async function loginAs(page: Page, email: string, password: string, home: RegExp) {
  await page.goto('/entrar');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Palavra-passe', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page).toHaveURL(home);
}

/** Dia da semana (en-GB, abreviado) na hora de Lisboa, o fuso do personal trainer semeado. */
export function weekdayInLisbon(date = new Date()): string {
  return new Intl.DateTimeFormat('en-GB', { weekday: 'short', timeZone: 'Europe/Lisbon' }).format(
    date
  );
}
