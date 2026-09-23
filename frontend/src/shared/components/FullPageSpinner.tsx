import { Skeleton } from '@/shared/components/ui/skeleton';

/** Ecrã de carregamento de página inteira. */
export function FullPageSpinner({ label }: { label?: string }) {
  return (
    <div
      aria-busy="true"
      aria-live="polite"
      className="bg-background flex min-h-dvh flex-col gap-4 p-6"
    >
      <span className="sr-only">{label}</span>
      <Skeleton className="h-10 w-64" />
      <Skeleton className="h-4 w-96" />
      <div className="grid gap-4 pt-4 sm:grid-cols-2 lg:grid-cols-3">
        <Skeleton className="h-32" />
        <Skeleton className="h-32" />
        <Skeleton className="h-32" />
      </div>
    </div>
  );
}
