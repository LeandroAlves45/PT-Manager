import { MEASUREMENT_LABELS } from '@/features/clients';
import type { components } from '@/shared/api/schema';
import { formatNumber } from '@/shared/lib/format';

type CheckIn = components['schemas']['CheckInResponse'];
type Measurements = components['schemas']['BodyMeasurementsPayload'];
type Feedback = components['schemas']['CheckInFeedbackPayload'];

/** Rótulos das respostas qualitativas ('CheckInFeedbackPayload'). */
export const FEEDBACK_LABELS: Readonly<Record<keyof Feedback, string>> = {
  appetite: 'Apetite',
  digestion: 'Digestão',
  training_load: 'Carga de treino',
  recovery_sleep: 'Recuperação e sono',
  energy_levels: 'Energia',
  body_response: 'Resposta do corpo',
};

/** Linha "rótulo: valor" de uma resposta, para a vista de leitura. */
export interface AnswerLine {
  readonly label: string;
  readonly value: string;
}

/**
 * Valores preenchidos de um check-in respondido, já formatados. Campos vazios ficam de fora:
 * a leitura mostra o que o cliente respondeu, não uma grelha de traços.
 */
export function answerLines(checkIn: CheckIn): {
  metrics: AnswerLine[];
  measurements: AnswerLine[];
  feedback: AnswerLine[];
} {
  const metrics: AnswerLine[] = [];

  if (checkIn.weight_kg != null)
    metrics.push({ label: 'Peso', value: `${formatNumber(checkIn.weight_kg, 1)} kg` });
  if (checkIn.body_fat_percentage != null)
    metrics.push({
      label: 'Massa gorda',
      value: `${formatNumber(checkIn.body_fat_percentage, 1)}%`,
    });
  if (checkIn.training_adherence_score != null)
    metrics.push({
      label: 'Adesão ao treino',
      value: `${checkIn.training_adherence_score}%`,
    });
  if (checkIn.nutrition_adherence_score != null)
    metrics.push({
      label: 'Adesão à alimentação',
      value: `${checkIn.nutrition_adherence_score}%`,
    });

  const measurements = (Object.keys(MEASUREMENT_LABELS) as (keyof Measurements)[]).flatMap(
    (key) => {
      const value = checkIn.body_measurements[key];
      return value === null
        ? []
        : [{ label: MEASUREMENT_LABELS[key], value: formatNumber(value, 1) }];
    }
  );

  const feedback = (Object.keys(FEEDBACK_LABELS) as (keyof Feedback)[]).flatMap((key) => {
    const value = checkIn.feedback[key];
    return value === null ? [] : [{ label: FEEDBACK_LABELS[key], value }];
  });

  if (checkIn.notes != null) feedback.push({ label: 'Notas', value: checkIn.notes });

  return { metrics, measurements, feedback };
}
