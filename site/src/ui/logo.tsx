import { cn } from '@/ui/cn';

/** Logótipo da marca (`public/logo.svg`, 1916×311). Largura/altura evitam layout shift. */
export function Logo({ className }: { className?: string }) {
  return (
    <img
      src="/logo.svg"
      alt="PT Manager"
      width={136}
      height={22}
      className={cn('block h-[22px] w-auto', className)}
    />
  );
}
