import { Check, Plus, X } from 'lucide-react';
import { useState } from 'react';

import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/shared/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/components/ui/popover';
import { MUSCLE_GROUPS, muscleGroupLabel, type MuscleGroupCode } from '@/shared/lib/muscleGroups';
import { cn } from '@/shared/lib/utils';

/**
 * Multi-seleção de grupos musculares: chips removíveis e uma lista fixa com
 * caixas, filtrada no browser (sem pedidos: a lista é a do `MuscleGroupCatalog`).
 *
 * Teclado: setas e Enter escolhem/retiram na lista, Escape fecha, e Backspace com o filtro
 * vazio retira o último grupo escolhido. Cada chip tem o seu botão "Remover …".
 *
 * Aceita os atributos do `FormField` (`id`, `aria-invalid`, `aria-describedby`), ligados ao
 * botão que abre a lista.
 */
export function MuscleGroupPicker({
  id,
  value,
  onChange,
  disabled = false,
  'aria-invalid': ariaInvalid,
  'aria-describedby': ariaDescribedBy,
}: {
  id?: string;
  value: readonly MuscleGroupCode[];
  onChange: (next: MuscleGroupCode[]) => void;
  disabled?: boolean;
  'aria-invalid'?: boolean;
  'aria-describedby'?: string | undefined;
}) {
  const [open, setOpen] = useState(false);
  const [filter, setFilter] = useState('');
  const chosen = new Set<MuscleGroupCode>(value);

  function toggle(code: MuscleGroupCode) {
    onChange(chosen.has(code) ? value.filter((item) => item !== code) : [...value, code]);
  }

  return (
    <div
      className={cn(
        'border-input flex min-h-10 flex-wrap items-center gap-1.5 rounded-md border p-1.5',
        ariaInvalid && 'border-destructive'
      )}
    >
      {value.map((code) => (
        <span
          key={code}
          className="bg-secondary text-secondary-foreground inline-flex items-center gap-1 rounded-full py-0.5 pr-0.5 pl-2.5 text-xs font-medium"
        >
          {muscleGroupLabel(code)}
          <button
            type="button"
            disabled={disabled}
            aria-label={`Remover ${muscleGroupLabel(code)}`}
            onClick={() => toggle(code)}
            className="hover:bg-accent focus-visible:ring-ring/50 inline-flex size-6 items-center justify-center rounded-full outline-none focus-visible:ring-[3px]"
          >
            <X aria-hidden className="size-3" />
          </button>
        </span>
      ))}
      <Popover
        open={open}
        onOpenChange={(next) => {
          setOpen(next);
          if (!next) setFilter('');
        }}
      >
        <PopoverTrigger asChild>
          <button
            id={id}
            type="button"
            role="combobox"
            aria-expanded={open}
            aria-invalid={ariaInvalid}
            aria-describedby={ariaDescribedBy}
            disabled={disabled}
            className="text-muted-foreground hover:text-foreground focus-visible:ring-ring/50 inline-flex min-h-8 items-center gap-1 rounded-md px-2 text-sm outline-none focus-visible:ring-[3px] disabled:cursor-not-allowed disabled:opacity-50"
          >
            <Plus aria-hidden className="size-4" />
            {value.length === 0 ? 'Escolher grupos musculares' : 'Adicionar'}
          </button>
        </PopoverTrigger>
        <PopoverContent className="w-72 p-0" align="start">
          <Command>
            <CommandInput
              placeholder="Filtrar grupos…"
              value={filter}
              onValueChange={setFilter}
              onKeyDown={(event) => {
                const last = value.at(-1);
                if (event.key === 'Backspace' && filter === '' && last !== undefined) {
                  event.preventDefault();
                  toggle(last);
                }
              }}
            />
            <CommandList className="max-h-80">
              <CommandEmpty>Nenhum grupo muscular com esse nome.</CommandEmpty>
              <CommandGroup>
                {MUSCLE_GROUPS.map((group) => {
                  const selected = chosen.has(group.code);
                  return (
                    <CommandItem
                      key={group.code}
                      value={group.label}
                      keywords={[group.code]}
                      // O cmdk usa aria-selected para o item em foco; a escolha anuncia-se no nome.
                      aria-label={selected ? `${group.label}, selecionado` : group.label}
                      className="min-h-11"
                      onSelect={() => toggle(group.code)}
                    >
                      <span
                        aria-hidden
                        className={cn(
                          'border-input flex size-4 items-center justify-center rounded-sm border',
                          selected && 'bg-primary border-primary text-primary-foreground'
                        )}
                      >
                        {selected && <Check className="size-3" />}
                      </span>
                      <span className="flex-1">{group.label}</span>
                      {selected && (
                        <span className="text-muted-foreground text-xs">selecionado</span>
                      )}
                    </CommandItem>
                  );
                })}
              </CommandGroup>
            </CommandList>
          </Command>
        </PopoverContent>
      </Popover>
    </div>
  );
}
