import { useMemo, useState } from 'react';
import { toast } from 'sonner';

import { useChangeTimezoneMutation } from '@/features/trainer-settings/api/settings';
import { settingsErrorMessage, timezoneOptions } from '@/features/trainer-settings/lib/settings';
import type { components } from '@/shared/api/schema';
import { Combobox, type ComboboxOption } from '@/shared/components/Combobox';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';

type Settings = components['schemas']['TrainerSettingsResponse'];

/**
 * Fuso horário IANA. Decide o "dia local" no servidor: o hoje do painel, o estado dos
 * check-ins e a regra de uma sessão por cliente por dia. Mudar pode ser recusado com
 * `trainer_settings_schedule_conflict` se dois agendamentos passarem a cair no mesmo dia.
 *
 * A lista vem do próprio browser (`Intl.supportedValuesOf`), sem dependência nova. São mais de
 * 400 fusos, por isso escolhe-se com pesquisa (ponto 11 da 6GB) e não num `<select>` nativo.
 * «Guardar fuso» só parece pronto quando o fuso mudou (ponto 12): sem alteração fica com o
 * aspeto secundário, para não parecer um botão premido.
 *
 * @param settings Definições atuais.
 */
export function TimezoneSection({ settings }: { settings: Settings }) {
  const mutation = useChangeTimezoneMutation();
  const [timezone, setTimezone] = useState(settings.timezone);
  const [error, setError] = useState<string | undefined>(undefined);
  const [search, setSearch] = useState('');
  const options = useMemo(() => timezoneOptions(settings.timezone), [settings.timezone]);
  const filtered = useMemo(() => filterTimezones(options, search), [options, search]);
  const dirty = timezone !== settings.timezone;

  async function save() {
    setError(undefined);
    try {
      await mutation.mutateAsync(timezone);
      toast.success('Fuso horário guardado com sucesso.');
    } catch (failure) {
      setError(settingsErrorMessage(failure, 'Não foi possível guardar o fuso horário.'));
    }
  }

  return (
    <section
      aria-labelledby="timezone-title"
      className="border-border space-y-4 rounded-xl border p-5"
    >
      <div>
        <h2 id="timezone-title" className="font-display text-xl">
          Fuso horário
        </h2>
        <p className="text-muted-foreground text-sm">
          Define o dia de hoje no painel, nas avaliações e nas sessões.
        </p>
      </div>
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
        className="flex flex-wrap items-end gap-3"
      >
        <FormField label="Fuso" error={error} className="w-full max-w-sm">
          {(control) => (
            <Combobox
              {...control}
              value={timezoneOption(timezone)}
              onChange={(option) => {
                setTimezone(option.value);
                setSearch('');
              }}
              search={search}
              onSearchChange={setSearch}
              options={filtered}
              placeholder="Escolhe um fuso…"
              searchPlaceholder="Pesquisar cidade ou região"
              emptyText="Nenhum fuso encontrado."
            />
          )}
        </FormField>
        <Button
          type="submit"
          variant={dirty ? 'default' : 'outline'}
          disabled={mutation.isPending || !dirty}
        >
          {mutation.isPending ? 'A guardar…' : 'Guardar fuso'}
        </Button>
      </form>
    </section>
  );
}

function timezoneOption(value: string): ComboboxOption {
  return { value, label: value.replaceAll('_', ' ') };
}

/**
 * Filtra os fusos pelo texto escrito, sem distinguir maiúsculas, acentos, «_» ou «/»:
 * «sao paulo» encontra «America/Sao_Paulo» e «lisboa» não encontra nada (os nomes IANA são em
 * inglês), por isso a pesquisa também aceita só a cidade («lisbon»).
 */
function filterTimezones(options: readonly string[], term: string): ComboboxOption[] {
  const needle = normalize(term);
  const matches =
    needle === '' ? options : options.filter((value) => normalize(value).includes(needle));
  return matches.map(timezoneOption);
}

function normalize(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/[_/]/g, ' ')
    .toLowerCase()
    .trim();
}
