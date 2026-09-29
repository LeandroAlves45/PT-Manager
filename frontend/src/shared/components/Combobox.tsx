import { Check, ChevronsUpDown } from 'lucide-react';
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
import { Skeleton } from '@/shared/components/ui/skeleton';
import { cn } from '@/shared/lib/utils';

/** Uma opção do 'Combobox': identificador, texto principal e linha secundária opcional. */
export interface ComboboxOption {
  readonly value: string;
  readonly label: string;
  readonly description?: string;
}

/**
 * Seletor com pesquisa no servidor (Popover + cmdk), para listas longas demais para um
 * `<select>` — clientes nesta fatia, catálogo de exercícios e alimentos.
 *
 * Não filtra no browser (`shouldFilter={false}`): quem chama liga `search` a uma query com
 * debounce e entrega as `options` já filtradas pelo servidor. Aceita os atributos do
 * `FormField` (`id`, `aria-invalid`, `aria-describedby`) para o rótulo e o erro ficarem
 * ligados ao botão que abre a lista.
 */
export function Combobox({
  id,
  value,
  onChange,
  search,
  onSearchChange,
  options,
  loading = false,
  placeholder,
  searchPlaceholder,
  emptyText,
  disabled = false,
  'aria-invalid': ariaInvalid,
  'aria-describedby': ariaDescribedBy,
}: {
  id?: string;
  value: ComboboxOption | null;
  onChange: (option: ComboboxOption) => void;
  search: string;
  onSearchChange: (term: string) => void;
  options: readonly ComboboxOption[];
  loading?: boolean;
  placeholder: string;
  searchPlaceholder: string;
  emptyText: string;
  disabled?: boolean;
  'aria-invalid'?: boolean;
  'aria-describedby'?: string | undefined;
}) {
  const [open, setOpen] = useState(false);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          id={id}
          type="button"
          role="combobox"
          aria-expanded={open}
          aria-invalid={ariaInvalid}
          aria-describedby={ariaDescribedBy}
          disabled={disabled}
          className={cn(
            'border-input bg-background dark:bg-input/30 flex h-9 w-full min-w-0 items-center justify-between gap-2 rounded-md border px-3 py-1 text-left text-base shadow-xs transition-[color,box-shadow] outline-none disabled:cursor-not-allowed disabled:opacity-50 md:text-sm',
            'focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]',
            'aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40',
            value === null && 'text-muted-foreground'
          )}
        >
          <span className="truncate">{value?.label ?? placeholder}</span>
          <ChevronsUpDown aria-hidden className="size-4 shrink-0 opacity-50" />
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-(--radix-popover-trigger-width) min-w-64 p-0" align="start">
        <Command shouldFilter={false}>
          <CommandInput
            placeholder={searchPlaceholder}
            value={search}
            onValueChange={onSearchChange}
          />
          <CommandList className="max-h-80">
            {loading ? (
              <div role="status" aria-label="A carregar…" className="space-y-2 p-2">
                {Array.from({ length: 3 }, (_, index) => (
                  <Skeleton key={index} className="h-11 w-full" />
                ))}
              </div>
            ) : (
              <>
                <CommandEmpty>{emptyText}</CommandEmpty>
                <CommandGroup>
                  {options.map((option) => (
                    <CommandItem
                      key={option.value}
                      value={option.value}
                      className="min-h-11"
                      onSelect={() => {
                        onChange(option);
                        setOpen(false);
                      }}
                    >
                      <Check
                        aria-hidden
                        className={cn(
                          'size-4',
                          value?.value === option.value ? 'opacity-100' : 'opacity-0'
                        )}
                      />
                      <span className="min-w-0 flex-1">
                        <span className="block truncate">{option.label}</span>
                        {option.description !== undefined && (
                          <span className="text-muted-foreground block truncate text-xs">
                            {option.description}
                          </span>
                        )}
                      </span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              </>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
