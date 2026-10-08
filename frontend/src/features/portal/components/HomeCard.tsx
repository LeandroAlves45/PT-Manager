import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import { Button } from "@/shared/components/ui/button";


/**
 * Cartão do Início do portal: título com ícone, conteúdo e, no máximo, uma ação.
 *
 * A ação é um link de largura total com 44 px de altura, o alvo de toque mínimo no
 * telemóvel.
 */
export function HomeCard({
  title,
  icon: Icon,
  action,
  children,
}: {
  title: string;
  icon: LucideIcon;
  action?: { readonly label: string; readonly to: string };
  children: ReactNode;
}) {
  return (
    <section
      aria-label={title}
      className="bg-card border-border flex flex-col gap-3 rounded-xl border p-4"
  >
    <h2 className="text-muted-foreground flex items-center gap-2 font-mono text-xs tracking-wide uppercase">
      <Icon aria-hidden className="text-primary size-4" />
      {title}
    </h2>
    <div className="flex-1 space-y-1">{children}</div>
    {action !== undefined && (
      <Button asChild className="min-h-11 w-full">
        <Link to={action.to}>{action.label}</Link>
      </Button>
    )}
  </section>
);
}
