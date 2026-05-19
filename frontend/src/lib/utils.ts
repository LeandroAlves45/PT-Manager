import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

/**
 * Combina classes CSS com tailwind-merge
 * 
 * útil para componentes reutilizáveis onde as classes podem ser passadas como props
 * permitir sobrescrever estilos padrão sem se preocupar com conflitos de classes
 * 
 * @param {...(string|object|Array)} inputs - Classes CSS a serem combinadas
 * @return {string} - Classes CSS combinadas e otimizadas
 */

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}