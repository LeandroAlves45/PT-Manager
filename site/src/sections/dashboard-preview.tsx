/**
 * Ecrãs reais da app: o painel do personal trainer e, a partir de tablet, a nutrição do cliente
 * no telemóvel por cima.
 *
 * São capturas da app (com dados de demonstração, nomes fictícios), não um desenho: o visitante
 * vê o que vai usar. `width` e `height` reservam o espaço (sem saltos ao carregar). O painel está
 * no hero e carrega logo; o telemóvel é decorativo no ecrã pequeno e só aparece em `md`.
 */
export function DashboardPreview() {
  return (
    <figure className="relative mt-[clamp(3rem,7vw,5rem)]">
      <div className="preview-shadow bg-card overflow-hidden rounded-2xl border">
        <img
          src="/app-painel-trainer.webp"
          width={1600}
          height={1000}
          alt="Painel do personal trainer no PT Manager: avaliações e check-ins por rever, packs de sessões a terminar, planos a expirar e as sessões de hoje."
          fetchPriority="high"
          decoding="async"
          className="block h-auto w-full"
        />
      </div>
      <img
        src="/app-nutricao-cliente.webp"
        width={780}
        height={1688}
        alt="Portal do cliente no telemóvel: metas do dia em kcal e macros, e as refeições do plano alimentar."
        loading="lazy"
        decoding="async"
        className="bg-background preview-shadow absolute right-[4%] -bottom-8 hidden h-auto w-[21%] max-w-56 rounded-[1.75rem] border-4 md:block"
      />
      <figcaption className="text-muted-foreground mt-4 text-center text-xs md:mt-14">
        Ecrãs reais da app, com dados de demonstração.
      </figcaption>
    </figure>
  );
}
