/**
 * Pré-visualização ilustrativa do painel da app.
 *
 * É uma imagem feita de markup: `role="img"` + `aria-label` descrevem-na como um todo e os
 * números fictícios não são lidos um a um. Leva a etiqueta "dados ilustrativos" à vista.
 */

const kpis = [
  { label: 'Clientes ativos', value: '24' },
  { label: 'Sessões esta semana', value: '18' },
  { label: 'Check-ins por rever', value: '3' },
] as const;

const bars = [
  'h-[34%]',
  'h-[42%]',
  'h-[38%]',
  'h-[55%]',
  'h-[50%]',
  'h-[62%]',
  'h-[58%]',
  'h-[70%]',
  'h-[66%]',
  'h-[78%]',
];

const recentClients = [
  { width: 'w-[38%]', status: 'Plano ativo', highlight: true },
  { width: 'w-[30%]', status: 'Convite enviado', highlight: false },
  { width: 'w-[44%]', status: 'Check-in novo', highlight: true },
  { width: 'w-[26%]', status: 'Pack 10 sessões', highlight: false },
] as const;

export function DashboardPreview() {
  return (
    <div
      role="img"
      aria-label="Pré-visualização ilustrativa do painel do PT Manager"
      className="preview-shadow from-card/95 to-background mt-[clamp(3rem,7vw,5rem)] rounded-t-2xl border border-b-0 bg-linear-to-b p-3 pb-0"
    >
      <div className="flex items-center gap-2.5 px-1.5 pt-1.5 pb-3.5">
        <img
          src="/favicon.svg"
          alt=""
          width={22}
          height={22}
          className="size-[22px] rounded-[5px]"
        />
        <span className="text-sm font-semibold">Painel</span>
        <span className="bg-background ml-3 hidden h-7 max-w-60 flex-1 rounded-lg border sm:block" />
        <span className="text-muted-foreground ml-auto font-mono text-xs tracking-[0.06em] uppercase">
          dados ilustrativos
        </span>
      </div>

      <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4">
        {kpis.map((kpi) => (
          <div key={kpi.label} className="bg-card rounded-xl border p-3.5">
            <div className="text-muted-foreground text-xs">{kpi.label}</div>
            <div className="tabular mt-1.5 text-[1.75rem] font-medium">{kpi.value}</div>
          </div>
        ))}
        <div className="bg-card border-primary-line rounded-xl border p-3.5">
          <div className="text-muted-foreground text-xs">Packs a terminar</div>
          <div className="tabular text-primary mt-1.5 text-[1.75rem] font-medium">2</div>
        </div>

        <div className="bg-card col-span-2 rounded-t-xl border border-b-0 px-3.5 pt-3.5">
          <div className="text-muted-foreground flex justify-between text-xs">
            <span>Volume de treino</span>
            <span className="font-mono">12 sem.</span>
          </div>
          <div className="mt-3.5 flex h-30 items-end gap-1.5">
            {bars.map((height, index) => (
              <div key={index} className={`bg-placeholder-soft flex-1 rounded-t ${height}`} />
            ))}
            <div className="bg-primary/45 h-[84%] flex-1 rounded-t" />
            <div className="bg-primary glow-primary h-[96%] flex-1 rounded-t" />
          </div>
        </div>

        <div className="bg-card col-span-2 rounded-t-xl border border-b-0 px-3.5 pt-3.5">
          <div className="text-muted-foreground text-xs">Clientes recentes</div>
          <div className="mt-3.5 flex flex-col gap-2.5 pb-4">
            {recentClients.map((client) => (
              <div key={client.status} className="flex items-center gap-2.5">
                <span className="bg-placeholder-soft size-7 rounded-full" />
                <span className={`bg-placeholder h-2 rounded ${client.width}`} />
                <span
                  className={
                    client.highlight
                      ? 'border-primary-line text-primary ml-auto rounded-full border px-2 py-0.5 text-xs'
                      : 'border-border-strong text-muted-foreground ml-auto rounded-full border px-2 py-0.5 text-xs'
                  }
                >
                  {client.status}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
