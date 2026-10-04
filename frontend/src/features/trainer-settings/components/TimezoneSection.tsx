import { useMemo, useState } from 'react';
import { toast } from 'sonner';

import { useChangeTimezoneMutation } from '@/features/trainer-settings/api/settings';
import { settingsErrorMessage, timezoneOptions } from '@/features/trainer-settings/lib/settings';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { NativeSelect } from '@/shared/components/ui/native-select';

type Settings = components['schemas']['TrainerSettingsResponse'];

/**
 * Fuso horário IANA. Decide o "dia local" no servidor: o hoje do painel, o estado dos
 * check-ins e a regra de uma sessão por cliente por dia. Mudar pode ser recusado com
 * `trainer_settings_schedule_conflict` se dois agendamentos passarem a cair no mesmo dia.
 *
 * A lista vem do próprio browser (`Intl.supportedValuesOf`), sem dependência nova.
 *
 * @param settings Definições atuais.
 */
export function TimezoneSection({ settings }: { settings: Settings }) {
  const mutation = useChangeTimezoneMutation();
  const [timezone, setTimezone] = useState(settings.timezone);
  const [error, setError] = useState<string | undefined>(undefined);
  const options = useMemo(() => timezoneOptions(settings.timezone), [settings.timezone]);

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
          Define o dia de hoje no painel, nos check-ins e nas sessões.
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
            <NativeSelect
              {...control}
              value={timezone}
              onChange={(event) => setTimezone(event.target.value)}
            >
              {options.map((value) => (
                <option key={value} value={value}>
                  {value.replaceAll('_', ' ')}
                </option>
              ))}
            </NativeSelect>
          )}
        </FormField>
        <Button type="submit" disabled={mutation.isPending || timezone === settings.timezone}>
          {mutation.isPending ? 'A guardar…' : 'Guardar fuso'}
        </Button>
      </form>
    </section>
  );
}
