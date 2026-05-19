/**
 * MuscleMultiSelect.jsx — dropdown multi-selecção de grupos musculares.
 *
 * Recebe e devolve uma string separada por vírgula (formato do backend).
 * Internamente converte para array para facilitar a lógica de selecção.
 *
 * Lista fixa: Peito, Costas, Ombros, Bicep, Tricep, Quadricep,
 *             Isquiotibiais, Glúteo, Gémeos, Abdominais, Trapézios
 *
 * @param {string}   value    - Valor actual: "Peito, Tricep"
 * @param {Function} onChange - Callback com nova string: "Peito, Tricep, Glúteo"
 * @param {string}   [error]  - Mensagem de erro para mostrar abaixo do trigger
 */

import { useState, useRef, useEffect } from 'react';
import { Badge } from '@/components/ui/badge';
import { ChevronDown, X } from 'lucide-react';

// Grupos de músculos organizados por categoria
const MUSCLE_GROUPS = {
  Peito: ['Peitoral Clavicular', 'Peitoral'],
  Costas: ['Grande Dorsal', 'Trapézio', 'Romboides', 'Eretores da Espinha'],
  Ombros: ['Deltóide Anterior', 'Deltóide Lateral', 'Deltóide Posterior'],
  Braços: ['Bicep', 'Tricep'],
  Pernas: ['Quadricep', 'Isquiotibiais', 'Glúteo', 'Gémeos'],
  Core: ['Abdominais', 'Oblíquos', 'Core'],
};

/**
 * Multi-select de músculos com dropdown agrupado por categoria.
 *
 * Recebe e devolve uma string separada por vírgula (formato do backend).
 * Internamente converte para array para facilitar a seleção.
 *
 * @param {string} value      - Valor atual: "Bicep, Tricep"
 * @param {Function} onChange - Callback com nova string: "Bicep, Tricep, Glúteo"
 * @param {string} [error]    - Mensagem de erro para mostrar
 */

export default function MuscleMultiSelect({ value = '', onChange, error }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  // Converte string "Bicep, Tricep" para array ['Bicep', 'Tricep']
  const selected = value
    ? value
        .split(',')
        .map((m) => m.trim())
        .filter(Boolean)
    : [];

  // Fecha dropdown ao clicar fora
  useEffect(() => {
    const handleClickOutside = (event) => {
      if (ref.current && !ref.current.contains(event.target)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, []);

  //Adciona ou remove músculo da seleção
  const toggleMuscle = (muscle) => {
    const next = selected.includes(muscle)
      ? selected.filter((m) => m !== muscle) //remove
      : [...selected, muscle]; //adiciona
    onChange(next.join(', ')); //devolve string "Bicep, Tricep"
  };

  const removeMuscle = (muscle, event) => {
    event.stopPropagation();
    toggleMuscle(muscle);
  };

  return (
    <div ref={ref} className="relative">

      {/* Trigger — mostra músculos selecionados como badges */}
      <div
        role="button"
        tabIndex={0}
        onClick={() => setOpen((o) => !o)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            setOpen((o) => !o);
          }
        }}
        className={`
          w-full min-h-10 px-3 py-2 rounded-md border bg-background text-left
          flex flex-wrap gap-1 items-center cursor-pointer
          ${error ? 'border-destructive' : 'border-input'}
          focus:outline-none focus:ring-1 focus:ring-ring
        `}
      >
        {selected.length === 0 ? (
          <span className="text-sm text-muted-foreground">
            Seleciona os músculos trabalhados...
          </span>
        ) : (
          selected.map((muscle) => (
            <Badge
              key={muscle}
              variant="secondary"
              className="gap-1 pr-1 text-xs"
            >
              {muscle}
              {/* X para remover músculo individual */}
              <button
                type="button"
                onClick={(e) => removeMuscle(muscle, e)}
                className="hover:text-destructive ml-0.5"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))
        )}
        <ChevronDown
          className={`h-4 w-4 text-muted-foreground ml-auto shrink-0 transition-transform ${open ? 'rotate-180' : ''}`}
        />
      </div>

      {/* Dropdown com lista flat de grupos musculares */}
      {open && (
        <div className="absolute z-50 mt-1 w-full rounded-md border border-border bg-popover shadow-lg overflow-hidden">
          <div className="max-h-60 overflow-y-auto p-1">
            {Object.entries(MUSCLE_GROUPS).map(([category, muscles]) => (
              <div key={category}>
                <p className="text-xs font-semibold text-muted-foreground px-3 py-1 mt-1">
                  {category}
                </p>
                {muscles.map((muscle) => {
                  const isSelected = selected.includes(muscle);
                  return (
                    <button
                      key={muscle}
                      type="button"
                      onClick={() => toggleMuscle(muscle)}
                      className={`
                        w-full text-left text-sm px-3 py-1.5 rounded-sm
                        flex items-center gap-2 transition-colors
                        ${
                          isSelected
                            ? 'bg-primary/10 text-primary font-medium'
                            : 'hover:bg-accent text-foreground'
                        }
                      `}
                    >
                      <span
                        className={`
                          h-4 w-4 rounded border shrink-0 flex items-center justify-center
                          ${isSelected ? 'bg-primary border-primary' : 'border-input'}
                        `}
                      >
                        {isSelected && (
                          <svg
                            viewBox="0 0 10 8"
                            className="h-2.5 w-2.5 text-primary-foreground fill-current"
                          >
                            <path
                              d="M1 4l3 3 5-6"
                              stroke="currentColor"
                              strokeWidth="1.5"
                              fill="none"
                              strokeLinecap="round"
                            />
                          </svg>
                        )}
                      </span>
                      {muscle}
                    </button>
                  );
                })}
              </div>
            ))}
          </div>

          {/* Rodapé com contagem + botão de limpar tudo */}
          {selected.length > 0 && (
            <div className="border-t border-border px-3 py-2 flex items-center justify-between">
              <span className="text-xs text-muted-foreground">
                {selected.length} músculo{selected.length !== 1 ? 's' : ''}{' '}
                selecionado{selected.length !== 1 ? 's' : ''}
              </span>
              <button
                type="button"
                onClick={() => onChange('')}
                className="text-xs text-destructive hover:underline"
              >
                Limpar tudo
              </button>
            </div>
          )}
        </div>
      )}

      {/* Mensagem de erro */}
      {error && <p className="text-xs text-destructive mt-1">{error}</p>}
    </div>
  );
}
