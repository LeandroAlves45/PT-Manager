import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import type * as Sonner from 'sonner';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { describeImportError } from '@/features/admin-catalog/lib/catalogImport';
import { API, restorableSession } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/render';

const toastMock = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', async (importOriginal) => ({
  ...(await importOriginal<typeof Sonner>()),
  toast: toastMock,
}));

const emptyPage = { items: [], total_count: 0, page_number: 1, page_size: 25 };
/**
 * Ficheiro do Node em vez do jsdom: o `fetch` do Vitest só converte o `FormData` do Node
 * (mesma solução dos testes do logo e da fotografia).
 */
async function nodeCsv(
  content = 'nome;proteina;hidratos;gordura\nOvo;12,6;1,1;10,6\n',
  name = 'alimentos.csv'
): Promise<File> {
  const { File: NodeFile } = (await import('node:' + 'buffer')) as { File: typeof File };
  const parsed = await new Response('', {
    headers: { 'content-type': 'application/x-www-form-urlencoded' },
  }).formData();
  vi.stubGlobal('FormData', parsed.constructor);
  return new NodeFile([content], name, { type: 'text/csv' });
}

afterEach(() => vi.unstubAllGlobals());

interface SentRequest {
  readonly dryRun: string | null;
  readonly fileName: string | null;
}

function catalogWithImport(
  respond: (sent: SentRequest) => Response,
  sent: SentRequest[] = [],
  kind: 'foods' | 'exercises' = 'foods'
) {
  const path = kind === 'foods' ? 'global-foods' : 'global-exercises';
  server.use(
    ...restorableSession({ role: 'superuser', trainer_id: null }),
    http.get(`${API}/${path}`, () => HttpResponse.json(emptyPage)),
    http.post(`${API}/${path}/import`, async ({ request }) => {
      expect(request.headers.get('content-type')).toMatch(/^multipart\/form-data; boundary=/);
      // Texto em vez de `formData()`: o parser do undici criaria um `File` do jsdom.
      const body = await request.text();
      const request_ = {
        dryRun: /name="dry_run"\r\n\r\n(true|false)/.exec(body)?.[1] ?? null,
        fileName: /name="file"; filename="([^"]+)"/.exec(body)?.[1] ?? null,
      };
      sent.push(request_);
      return respond(request_);
    })
  );
  return sent;
}

async function openDialog(kind: 'foods' | 'exercises' = 'foods') {
  const user = userEvent.setup();
  renderApp({ initialEntries: [`/admin/catalog/${kind}`] });
  await user.click(await screen.findByRole('button', { name: /Importar CSV/ }));
  const dialog = await screen.findByRole('dialog');
  return { user, dialog };
}

describe('CatalogImportDialog', () => {
  it('verifies first, then imports the same file and closes with a summary', async () => {
    const sent = catalogWithImport(({ dryRun }) =>
      HttpResponse.json({
        dry_run: dryRun === 'true',
        total_rows: 2,
        created: 1,
        skipped: [{ line: 3, name: 'Arroz', reason: 'already_exists' }],
      })
    );
    const { user, dialog } = await openDialog();
    const importButton = within(dialog).getByRole('button', { name: 'Importar' });

    await user.upload(within(dialog).getByLabelText('Ficheiro CSV'), await nodeCsv());
    expect(importButton).toBeDisabled();

    await user.click(within(dialog).getByRole('button', { name: 'Verificar' }));
    expect(await within(dialog).findByRole('status')).toHaveTextContent(
      'Ficheiro válido: 2 linhas. 1 alimentos a criar, 1 saltados.'
    );
    expect(within(dialog).getByText(/Linha 3: Arroz \(Já existe no catálogo\)/)).toBeVisible();
    expect(importButton).toBeEnabled();

    await user.click(importButton);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(toastMock.success).toHaveBeenCalledWith('1 alimentos importados, 1 saltados.');
    expect(sent).toEqual([
      { dryRun: 'true', fileName: 'alimentos.csv' },
      { dryRun: 'false', fileName: 'alimentos.csv' },
    ]);
  }, 15000);

  it('lists line errors in Portuguese and keeps import blocked', async () => {
    catalogWithImport(() =>
      HttpResponse.json(
        {
          title: 'validation_failed',
          detail: 'One or more validation errors occurred.',
          status: 400,
          errors: [
            { field: 'Rows[4].proteina', code: 'catalog_import_number_invalid', message: 'x' },
            { field: 'File', code: 'catalog_import_column_unknown', message: 'x' },
          ],
        },
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } }
      )
    );
    const { user, dialog } = await openDialog();

    await user.upload(within(dialog).getByLabelText('Ficheiro CSV'), await nodeCsv());
    await user.click(within(dialog).getByRole('button', { name: 'Verificar' }));

    const alert = await within(dialog).findByRole('alert');
    expect(alert).toHaveTextContent('2 erros encontrados');
    const rows = within(alert).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual([
      '4ProteínaNão é um número válido. Usa, por exemplo, 25,5.',
      'Ficheiro-O cabeçalho tem uma coluna que não é reconhecida.',
    ]);
    expect(within(dialog).getByRole('button', { name: 'Importar' })).toBeDisabled();
  }, 15000);

  it('requires a new verification after choosing another file', async () => {
    catalogWithImport(() =>
      HttpResponse.json({ dry_run: true, total_rows: 1, created: 1, skipped: [] })
    );
    const { user, dialog } = await openDialog();
    const input = within(dialog).getByLabelText('Ficheiro CSV');

    await user.upload(input, await nodeCsv());
    await user.click(within(dialog).getByRole('button', { name: 'Verificar' }));
    await within(dialog).findByRole('status');
    expect(within(dialog).getByRole('button', { name: 'Importar' })).toBeEnabled();

    await user.upload(input, new File(['nome;proteina\n'], 'outro.csv', { type: 'text/csv' }));
    expect(within(dialog).getByRole('button', { name: 'Importar' })).toBeDisabled();
    expect(within(dialog).queryByRole('status')).not.toBeInTheDocument();
  }, 15000);

  it('keeps import blocked when every row already exists', async () => {
    catalogWithImport(() =>
      HttpResponse.json({
        dry_run: true,
        total_rows: 1,
        created: 0,
        skipped: [{ line: 2, name: 'Ovo', reason: 'already_exists' }],
      })
    );
    const { user, dialog } = await openDialog();

    await user.upload(within(dialog).getByLabelText('Ficheiro CSV'), await nodeCsv());
    await user.click(within(dialog).getByRole('button', { name: 'Verificar' }));

    expect(await within(dialog).findByRole('status')).toHaveTextContent('0 alimentos a criar');
    expect(within(dialog).getByRole('button', { name: 'Importar' })).toBeDisabled();
  }, 15000);

  it('posts exercises to their own endpoint', async () => {
    const sent = catalogWithImport(
      () => HttpResponse.json({ dry_run: true, total_rows: 1, created: 1, skipped: [] }),
      [],
      'exercises'
    );
    const { user, dialog } = await openDialog('exercises');
    expect(within(dialog).getByRole('heading', { name: 'Importar exercícios' })).toBeVisible();

    await user.upload(within(dialog).getByLabelText('Ficheiro CSV'), await nodeCsv());
    await user.click(within(dialog).getByRole('button', { name: 'Verificar' }));

    await within(dialog).findByRole('status');
    expect(sent).toHaveLength(1);
  }, 15000);

  it('is not offered for supplements', async () => {
    server.use(
      ...restorableSession({ role: 'superuser', trainer_id: null }),
      http.get(`${API}/global-supplements`, () => HttpResponse.json(emptyPage))
    );
    renderApp({ initialEntries: ['/admin/catalog/supplements'] });

    expect(await screen.findByRole('link', { name: /Novo suplemento/ })).toBeVisible();
    expect(screen.queryByRole('button', { name: /Importar CSV/ })).not.toBeInTheDocument();
  }, 15000);
});

describe('describeImportError', () => {
  it('maps row fields to line, column label and message', () => {
    expect(
      describeImportError({
        field: 'Rows[12].macros',
        code: 'food_macros_exceed_total',
        message: 'x',
      })
    ).toEqual({
      line: 12,
      column: 'Proteína + hidratos + gordura',
      message: 'Não podem somar mais de 100 g.',
    });
  });

  it('falls back to the server message for unknown codes and whole-row errors', () => {
    expect(
      describeImportError({ field: 'Rows[3]', code: 'something_new', message: 'Server text' })
    ).toEqual({ line: 3, column: null, message: 'Server text' });
  });
});
