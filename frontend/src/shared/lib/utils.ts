import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Junta classes condicionais e resolve conflitos de Tailwind.
 *
 * `clsx` trata dos condicionais; `twMerge` garante que a última classe vence quando duas
 * atacam a mesma propriedade (`p-2 p-4` → `p-4`), o que é indispensável quando um
 * componente aceita `className` de fora.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
