import type { ReactNode } from 'react';

interface SectionHeadingProps {
  /** Id do `<h2>`, referenciado pelo `aria-labelledby` da secção. */
  id: string;
  eyebrow: string;
  children: ReactNode;
  className?: string;
}

/** Sobretítulo mono + título display de uma secção. */
export function SectionHeading({ id, eyebrow, children, className }: SectionHeadingProps) {
  return (
    <div className={className}>
      <p className="text-primary font-mono text-xs tracking-[0.12em] uppercase">{eyebrow}</p>
      <h2
        id={id}
        className="font-display mt-3.5 max-w-[18ch] text-[clamp(2.25rem,5.4vw,4.25rem)] leading-[0.92] text-balance uppercase"
      >
        {children}
      </h2>
    </div>
  );
}
