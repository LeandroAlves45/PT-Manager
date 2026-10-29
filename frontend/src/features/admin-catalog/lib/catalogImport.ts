import type { ApiFieldError } from '@/shared/api/problem';

/** Catálogos que aceitam importação por CSV (os suplementos criam-se à mão). */
export type ImportableCatalogKind = 'foods' | 'exercises';

/**
 * Modelo descarregável, no formato do Excel em português: separador `;` e decimais com
 * vírgula. A linha de exemplo mostra o formato de cada coluna.
 */
export const IMPORT_TEMPLATES: Readonly<Record<ImportableCatalogKind, string>> = {
  foods:
    'nome;descricao;proteina;hidratos;gordura;fibra;porcao_g\n' +
    'Arroz agulha cozido;Valores por 100 g;2,7;28,2;0,3;0,4;150\n',
  exercises:
    'nome;descricao;grupos_musculares;equipamento;dificuldade\n' +
    'Supino plano com barra;Deitado no banco, descer a barra ao peito;chest,triceps,shoulders;Barra;Intermédio\n',
};

/** Rótulo de cada coluna do CSV, para os erros por linha. */
const COLUMN_LABELS: Readonly<Record<string, string>> = {
  nome: 'Nome',
  descricao: 'Descrição',
  proteina: 'Proteína',
  hidratos: 'Hidratos',
  gordura: 'Gordura',
  fibra: 'Fibra',
  porcao_g: 'Porção (g)',
  macros: 'Proteína + hidratos + gordura',
  grupos_musculares: 'Grupos musculares',
  equipamento: 'Equipamento',
  dificuldade: 'Dificuldade',
};

/**
 * Mensagens PT-PT por código. O backend manda a mensagem em inglês; só o código é estável.
 */
const IMPORT_ERROR_MESSAGES: Readonly<Record<string, string>> = {
  catalog_import_file_too_large: 'O ficheiro tem mais de 1 MB.',
  catalog_import_file_empty: 'O ficheiro não tem cabeçalho ou não tem linhas de dados.',
  catalog_import_encoding_invalid:
    'O ficheiro não está em UTF-8. No Excel, grava como "CSV UTF-8 (delimitado por vírgulas)".',
  catalog_import_quote_unterminated: 'Há aspas abertas que nunca fecham a partir desta linha.',
  catalog_import_column_missing: 'Falta uma coluna obrigatória no cabeçalho.',
  catalog_import_column_unknown: 'O cabeçalho tem uma coluna que não é reconhecida.',
  catalog_import_column_duplicate: 'O cabeçalho tem uma coluna repetida.',
  catalog_import_too_many_rows: 'O ficheiro tem mais de 1000 linhas. Divide-o em partes.',
  catalog_import_row_columns: 'A linha não tem o mesmo número de colunas que o cabeçalho.',
  catalog_import_number_invalid: 'Não é um número válido. Usa, por exemplo, 25,5.',
  catalog_import_number_required: 'Indica um número.',
  catalog_import_name_formula: 'O nome não pode começar por =, +, - ou @.',
  catalog_import_difficulty_invalid: 'Usa Iniciante, Intermédio ou Avançado.',
  food_name_required: 'Indica o nome do alimento.',
  food_name_too_long: 'O nome não pode exceder 255 caracteres.',
  food_protein_out_of_range: 'Tem de estar entre 0 e 100 g.',
  food_carbs_out_of_range: 'Tem de estar entre 0 e 100 g.',
  food_fats_out_of_range: 'Tem de estar entre 0 e 100 g.',
  food_fiber_out_of_range: 'Tem de estar entre 0 e 100 g.',
  food_macros_exceed_total: 'Não podem somar mais de 100 g.',
  food_default_serving_out_of_range:
    'Tem de ser maior que 0 e até 1000 g, com até duas casas decimais.',
  exercise_name_required: 'Indica o nome do exercício.',
  exercise_name_too_long: 'O nome não pode exceder 255 caracteres.',
  exercise_muscle_groups_too_long: 'Tem demasiados grupos musculares.',
  exercise_muscle_groups_invalid: 'Usa só os códigos da lista (por exemplo chest,triceps).',
  exercise_equipment_too_long: 'O equipamento não pode exceder 255 caracteres.',
  exercise_difficulty_too_long: 'A dificuldade não pode exceder 50 caracteres.',
};

/** Motivo de uma linha válida não ter sido criada. */
export const SKIP_REASON_LABELS: Readonly<Record<string, string>> = {
  already_exists: 'Já existe no catálogo',
  duplicate_in_file: 'Repetido no ficheiro',
};

/** Erro de importação pronto a mostrar: linha (ou ficheiro), coluna e mensagem. */
export interface ImportErrorRow {
  readonly line: number | null;
  readonly column: string | null;
  readonly message: string;
}

const ROW_FIELD = /^Rows\[(\d+)\](?:\.(.+))?$/;

/**
 * Traduz um erro de campo da API. `Rows[12].proteina` passa a linha 12, coluna Proteína;
 * `File` é um erro do ficheiro inteiro, sem linha.
 */
export function describeImportError(error: ApiFieldError): ImportErrorRow {
  const match = ROW_FIELD.exec(error.field);
  const column = match?.[2] ?? null;

  return {
    line: match ? Number(match[1]) : null,
    column: column === null ? null : (COLUMN_LABELS[column] ?? column),
    message: IMPORT_ERROR_MESSAGES[error.code] ?? error.message,
  };
}
