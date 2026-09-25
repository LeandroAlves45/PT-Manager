import { Check } from 'lucide-react';

/** Visto dos benefícios dos planos (decorativo: o texto ao lado já diz tudo). */
export function CheckIcon() {
  return (
    <Check
      aria-hidden="true"
      className="text-primary mt-0.5 size-[18px] flex-none"
      strokeWidth={2.4}
    />
  );
}
