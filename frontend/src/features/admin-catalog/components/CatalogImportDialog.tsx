import { Download, Upload } from 'lucide-react';
import { useId, useState } from 'react';
import { toast } from 'sonner';

import {
  useCatalogImportMutation,
  type CatalogImportReport,
} from '@/features/admin-catalog/api/catalogImport';
import {
  describeImportError,
  IMPORT_TEMPLATES,
  SKIP_REASON_LABELS,
  type ImportableCatalogKind,
  type ImportErrorRow,
} from '@/features/admin-catalog/lib/catalogImport';
import { isApiProblem } from '@/shared/api/problem';
import { Button } from '@/shared/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/shared/components/ui/dialog';
import { formatNumber } from '@/shared/lib/format';

/** Faz o Excel abrir o modelo como UTF-8 e manter os acentos. */
const UTF8_BOM = String.fromCharCode(0xfeff);

const NOUNS: Record<ImportableCatalogKind, { plural: string; file: string }> = {
  foods: { plural: 'alimentos', file: 'modelo-alimentos.csv' },
  exercises: { plural: 'exercícios', file: 'modelo-exercicios.csv' },
};

/** Verificação feita a um ficheiro concreto; a importação só abre para esse mesmo ficheiro. */
interface Preview {
  readonly file: File;
  readonly report: CatalogImportReport;
}

/**
 * Importação CSV do catálogo global em dois passos: "Verificar" valida tudo sem gravar e
 * mostra os erros por linha; "Importar" só fica disponível depois de uma verificação limpa
 * do mesmo ficheiro, e grava tudo ou nada.
 */
export function CatalogImportDialog({ kind }: { kind: ImportableCatalogKind }) {
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [errors, setErrors] = useState<readonly ImportErrorRow[]>([]);
  const [failure, setFailure] = useState<string | null>(null);
  const mutation = useCatalogImportMutation(kind);
  const inputId = useId();
  const noun = NOUNS[kind];
  const canImport = preview !== null && preview.file === file && preview.report.created > 0;

  function reset() {
    setFile(null);
    setPreview(null);
    setErrors([]);
    setFailure(null);
  }

  function chooseFile(next: File | null) {
    reset();
    setFile(next);
  }

  function downloadTemplate() {
    const blob = new Blob([UTF8_BOM + IMPORT_TEMPLATES[kind]], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = noun.file;
    link.click();
    URL.revokeObjectURL(url);
  }

  async function send(dryRun: boolean) {
    if (file === null) return;
    setErrors([]);
    setFailure(null);

    try {
      const report = await mutation.mutateAsync({ file, dryRun });
      if (dryRun) {
        setPreview({ file, report });
        return;
      }

      toast.success(
        `${formatNumber(report.created)} ${noun.plural} importados` +
          (report.skipped.length > 0 ? `, ${formatNumber(report.skipped.length)} saltados.` : '.')
      );
      setOpen(false);
      reset();
    } catch (error) {
      setPreview(null);
      if (isApiProblem(error) && error.hasFieldErrors) {
        setErrors(error.fieldErrors.map(describeImportError));
      } else if (isApiProblem(error) && error.status === 400) {
        setFailure('O ficheiro não foi aceite. Confirma que é um CSV com menos de 1 MB.');
      } else {
        setFailure('Não foi possível processar o ficheiro. Tenta novamente.');
      }
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) reset();
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" className="min-h-11 md:min-h-8">
          <Upload aria-hidden /> Importar CSV
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Importar {noun.plural}</DialogTitle>
          <DialogDescription>
            Usa o modelo (separador ponto e vírgula, decimais com vírgula). Verifica primeiro: nada
            é gravado enquanto houver erros, e os nomes que já existem são saltados.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <Button variant="link" className="h-auto px-0" onClick={downloadTemplate}>
            <Download aria-hidden /> Descarregar modelo
          </Button>

          <div className="space-y-1.5">
            <label htmlFor={inputId} className="text-sm font-medium">
              Ficheiro CSV
            </label>
            <input
              id={inputId}
              type="file"
              accept=".csv,text/csv"
              className="file:bg-muted file:text-foreground block w-full text-sm file:mr-3 file:rounded-md file:border-0 file:px-3 file:py-2"
              onChange={(event) => chooseFile(event.target.files?.[0] ?? null)}
            />
          </div>

          {failure !== null && (
            <p role="alert" className="text-destructive text-sm">
              {failure}
            </p>
          )}

          {errors.length > 0 && (
            <div role="alert" className="space-y-2">
              <p className="text-destructive text-sm font-medium">
                {formatNumber(errors.length)} {errors.length === 1 ? 'erro' : 'erros'} encontrados.
                Corrige o ficheiro e verifica de novo.
              </p>
              <div className="border-border max-h-64 overflow-auto rounded-lg border">
                <table className="w-full text-sm">
                  <thead className="bg-muted sticky top-0 text-left">
                    <tr>
                      <th scope="col" className="p-2">
                        Linha
                      </th>
                      <th scope="col" className="p-2">
                        Coluna
                      </th>
                      <th scope="col" className="p-2">
                        Problema
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {errors.map((error, index) => (
                      <tr key={index} className="border-border border-t">
                        <td className="p-2 tabular-nums">{error.line ?? 'Ficheiro'}</td>
                        <td className="p-2">{error.column ?? '-'}</td>
                        <td className="p-2">{error.message}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {preview !== null && preview.file === file && (
            <div role="status" className="bg-muted space-y-2 rounded-lg p-3 text-sm">
              <p>
                Ficheiro válido: {formatNumber(preview.report.total_rows)} linhas.{' '}
                <strong>{formatNumber(preview.report.created)}</strong> {noun.plural} a criar
                {preview.report.skipped.length > 0 &&
                  `, ${formatNumber(preview.report.skipped.length)} saltados`}
                .
              </p>
              {preview.report.skipped.length > 0 && (
                <ul className="text-muted-foreground max-h-40 list-disc overflow-auto pl-5">
                  {preview.report.skipped.map((row) => (
                    <li key={row.line}>
                      Linha {row.line}: {row.name} ({SKIP_REASON_LABELS[row.reason] ?? row.reason})
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            disabled={file === null || mutation.isPending}
            onClick={() => void send(true)}
          >
            {mutation.isPending && mutation.variables.dryRun ? 'A verificar…' : 'Verificar'}
          </Button>
          <Button disabled={!canImport || mutation.isPending} onClick={() => void send(false)}>
            {mutation.isPending && !mutation.variables.dryRun ? 'A importar…' : 'Importar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
