/**
 * Grupos musculares aceites pelo backend, pela
 * ordem canónica em que o servidor os grava, com o rótulo PT-PT de cada código.
 *
 * Fonte única de rótulos: o catálogo do admin e a biblioteca do personal trainer usam esta lista.
 * O servidor recusa qualquer código fora dela (`exercise_muscle_groups_invalid`).
 */
export const MUSCLE_GROUPS = [
  { code: 'chest', label: 'Peito' },
  { code: 'back', label: 'Costas' },
  { code: 'lats', label: 'Dorsais' },
  { code: 'traps', label: 'Trapézio' },
  { code: 'lower_back', label: 'Lombar' },
  { code: 'shoulders', label: 'Ombros' },
  { code: 'biceps', label: 'Bíceps' },
  { code: 'triceps', label: 'Tríceps' },
  { code: 'forearms', label: 'Antebraços' },
  { code: 'core', label: 'Core' },
  { code: 'glutes', label: 'Glúteos' },
  { code: 'quadriceps', label: 'Quadríceps' },
  { code: 'hamstrings', label: 'Isquiotibiais' },
  { code: 'calves', label: 'Gémeos' },
] as const;

export type MuscleGroupCode = (typeof MUSCLE_GROUPS)[number]['code'];

const LABELS = new Map<string, string>(MUSCLE_GROUPS.map((group) => [group.code, group.label]));

/** Separa o texto gravado (`"chest,triceps"`) em códigos, tal como o servidor o normaliza. */
function splitCodes(value: string | null): string[] {
  if (value === null) return [];

  return value
    .split(',')
    .map((code) => code.trim().toLowerCase())
    .filter((code) => code !== '');
}

/**
 * Códigos conhecidos de um valor gravado, sem repetidos e pela ordem canónica.
 *
 * Um código desconhecido (texto livre anterior à lista fechada) fica de fora: o servidor
 * já não o aceitaria, por isso gravar o exercício de novo remove-o.
 */
export function parseMuscleGroups(value: string | null): MuscleGroupCode[] {
  const requested = new Set(splitCodes(value));
  return MUSCLE_GROUPS.map((group) => group.code).filter((code) => requested.has(code));
}

/** Valor a enviar para a API: códigos pela ordem canónica separados por vírgulas, ou 'null'. */
export function serializeMuscleGroups(codes: MuscleGroupCode[]): string | null {
  const chosen = new Set(codes);
  const ordered = MUSCLE_GROUPS.map((group) => group.code).filter((code) => chosen.has(code));

  return ordered.length === 0 ? null : ordered.join(',');
}

/** Rótulo de um código; um código desconhecido aparece como foi gravado. */
export function muscleGroupLabel(code: string): string {
  return LABELS.get(code) ?? code;
}

/** Texto para listas: `"chest,triceps"` -> `"Peito,Tríceps"`; vazio -> `"—"`. */
export function formatMuscleGroups(value: string | null): string {
  const codes = splitCodes(value);
  return codes.length === 0 ? '—' : codes.map(muscleGroupLabel).join(', ');
}
