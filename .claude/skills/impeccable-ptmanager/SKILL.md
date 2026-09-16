---
name: impeccable-ptmanager
description: |
  Design excellence for PT Manager frontend. Use when building UI components, pages, or modifying styles in React 19 + TypeScript + Tailwind CSS v4 + shadcn/ui. Triggers on: creating new pages, building components, adjusting layouts, styling forms, designing dashboards, reviewing UI/UX, improving accessibility. Delivers cohesive, accessible, performance-optimized design that respects PT Manager's brand and user patterns.
---

# Impeccable: Design Skill for PT Manager Frontend

You are designing user interfaces for PT Manager — a SaaS dashboard for personal trainers. Every component, page, and interaction should feel intentional, consistent, and accessible.

Canonical references (read before designing):
- `.claude/project/frontend/03_DESIGN_SYSTEM_E_MARCA.md` — brand tokens, typography, key components
- `.claude/project/frontend/02_CONVENCOES.md` — code conventions
- `.claude/project/frontend/00_ARQUITETURA_FRONTEND.md` — folder structure

**Stack: shadcn/ui (Radix) + Tailwind CSS v4 + TypeScript. Chakra UI is NOT used — never import `@chakra-ui/*`.**

## Design Principles for PT Manager

### 1. Clarity Over Decoration

PT Manager serves busy personal trainers. They need information fast. Motion and glass effects are allowed only when they reinforce hierarchy or state.

- **Information hierarchy:** Largest text for what matters most. Related data grouped visually.
- **White space:** Breathing room between sections. Dense data tables still need padding.
- **Color:** Purposeful. Brand blue (`--primary`) for primary actions, `--destructive` for destructive, `--success` for success. No random accent colors.
- **Typography:** Display font only for headings/brand; UI font for everything else. 16px base. Never below 12px.

### 2. Accessibility is Not Optional

Every component must:
- Contrast: WCAG AA minimum (4.5:1 for text, 3:1 for graphics). Brand blue `#00A3E9` fails with white text — use dark foreground on it (see design system doc).
- Keyboard navigation: Tab through interactive elements, Enter/Space to activate, Escape closes overlays
- Screen readers: `aria-label`, `aria-describedby`, semantic HTML (`<button>`, `<nav>`, `<main>`)
- Color blind safe: Don't rely on color alone (icon + text for status)
- Touch targets: 44px minimum (mobile), 32px minimum (desktop)

shadcn/ui components are built on Radix and already handle focus management and ARIA — keep them, don't rebuild primitives:
```tsx
<Button variant="destructive" aria-label="Apagar cliente">
  <Trash2 aria-hidden /> Apagar
</Button>
```

### 3. Consistency: Build Once, Use Everywhere

- **`src/shared/components/ui/`:** shadcn primitives (owned code, edit carefully)
- **`src/shared/components/`:** composed app components (DataTable, PageHeader, Combobox, EmptyState)
- **`src/features/*/components/`:** feature-specific components
- **Tailwind:** layout/spacing via utilities; colors only through semantic tokens (`bg-card`, `text-muted-foreground`)
- **Never duplicate:** If a pattern exists in `shared/`, use it.

### 4. Responsive Design: Mobile-First

The client portal is used on phones in the gym. Every page must work at 375px width.

```tsx
// Stack on mobile, row on desktop
<div className="flex flex-col gap-4 md:flex-row">
  <section className="flex-1">Lista de clientes</section>
  <section className="flex-1">Detalhe</section>
</div>
```

**Breakpoints (Tailwind v4 defaults):** `sm` 640px · `md` 768px · `lg` 1024px · `xl` 1280px · `2xl` 1536px

### 5. Dark/Light Mode Support

PT Manager ships a theme toggle (dark and light). Every color must work in both.

- Colors come from CSS variables in `globals.css` (`:root` and `.dark`), exposed to Tailwind via `@theme inline`
- Use semantic utilities (`bg-background`, `text-foreground`, `border-border`); use `dark:` only for exceptions
- Test in both modes before shipping

## Component Patterns

### Form Fields: react-hook-form + zod + shadcn Form

```tsx
const schema = z.object({ full_name: z.string().min(1, 'Nome obrigatório') });

export function ClientNameForm({ onSubmit }: { onSubmit: (v: z.infer<typeof schema>) => void }) {
  const form = useForm({ resolver: zodResolver(schema), defaultValues: { full_name: '' } });
  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <FormField
          control={form.control}
          name="full_name"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Nome completo</FormLabel>
              <FormControl><Input placeholder="João Silva" {...field} /></FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </form>
    </Form>
  );
}
```

Server validation errors (ProblemDetails `errors[]` with `field`) are mapped to `form.setError(field, …)`.

### Cards: Data Containers

```tsx
export function ClientCard({ client, onEdit }: ClientCardProps) {
  return (
    <Card className="transition-shadow hover:shadow-md">
      <CardHeader>
        <CardTitle>{client.full_name}</CardTitle>
        <CardDescription>{client.email}</CardDescription>
      </CardHeader>
      <CardFooter>
        <Button size="sm" variant="outline" onClick={onEdit}>Editar</Button>
      </CardFooter>
    </Card>
  );
}
```

**Avoid:** arbitrary values (`p-[13px]`), custom shadows outside the token scale, hover states without `transition-*`.

### Tables: Readable Data

Use the shared `DataTable` (TanStack Table + shadcn `Table`) for anything paginated by the server. For small static lists:

```tsx
<div className="overflow-x-auto">
  <Table>
    <TableHeader>
      <TableRow><TableHead>Data</TableHead><TableHead>Duração</TableHead><TableHead>Estado</TableHead></TableRow>
    </TableHeader>
    <TableBody>
      {sessions.map((s) => (
        <TableRow key={s.id}>
          <TableCell>{formatDate(s.starts_at)}</TableCell>
          <TableCell>{s.duration_minutes} min</TableCell>
          <TableCell><StatusBadge status={s.status} /></TableCell>
        </TableRow>
      ))}
    </TableBody>
  </Table>
</div>
```

### Dialogs: Intentional

```tsx
<AlertDialog>
  <AlertDialogTrigger asChild><Button variant="destructive">Arquivar</Button></AlertDialogTrigger>
  <AlertDialogContent>
    <AlertDialogHeader>
      <AlertDialogTitle>Arquivar cliente?</AlertDialogTitle>
      <AlertDialogDescription>Pode reativá-lo mais tarde.</AlertDialogDescription>
    </AlertDialogHeader>
    <AlertDialogFooter>
      <AlertDialogCancel>Cancelar</AlertDialogCancel>
      <AlertDialogAction onClick={onConfirm}>Arquivar</AlertDialogAction>
    </AlertDialogFooter>
  </AlertDialogContent>
</AlertDialog>
```

### Selection from catalogs: Combobox

Muscles, foods, exercises, supplements are chosen with the shared `Combobox` (Popover + Command), never a plain `<select>`:
- Short fixed lists (muscles): client-side filter, multi-select with chips.
- Large paginated catalogs (foods, exercises): server search with debounce via React Query.

## Performance: Keep Pages Snappy

- **Images:** `loading="lazy"` below the fold, explicit `width`/`height`
- **Code splitting:** lazy routes per feature
- **Avoid re-renders:** `useMemo`/`useCallback` only when the profiler shows real impact
- **Bundle size:** import icons individually from `lucide-react`; no whole-library imports

## PT Manager-Specific Patterns

### Navigation: Consistent Structure

Sidebar (collapsible) + topbar with breadcrumbs + ⌘K command menu. Nav items come from `shared/config/navigation.ts` per role — never hardcode links in layouts.

### Status Badges: Semantic Colors

```tsx
const statusVariant = {
  scheduled: 'info',
  completed: 'success',
  cancelled: 'destructive',
  no_show: 'warning',
} as const satisfies Record<SessionStatus, BadgeVariant>;

<Badge variant={statusVariant[status]}><StatusIcon status={status} />{t(status)}</Badge>
```

## Color Palette (PT Manager)

Semantic tokens defined in `globals.css` (values in the design system doc):

- **Primary actions:** `primary` (brand blue `#00A3E9` family)
- **Success:** `success` · **Warning:** `warning` · **Destructive:** `destructive` · **Info:** `info`
- **Neutral:** `background`, `card`, `muted`, `border`, `foreground`, `muted-foreground`

**Don't hardcode colors:**
```tsx
// Good
<Button>Criar sessão</Button>
// Avoid
<button className="bg-[#1e90ff]">Criar sessão</button>
```

## Checklist: Before Shipping a Component

- ✓ Semantic HTML (`<button>`, `<label>`, `<nav>`, not all `<div>`)
- ✓ ARIA labels on icons and screen-reader-only text
- ✓ Keyboard accessible (Tab, Enter, Space, Escape where needed)
- ✓ Works at 375px width (mobile)
- ✓ Responsive: test on mobile, tablet, desktop
- ✓ Dark and light mode: toggle and verify
- ✓ Touch target 44px+ (mobile), 32px+ (desktop)
- ✓ Contrast 4.5:1 (text/background)
- ✓ Color + icon/text for status (not color alone)
- ✓ No hardcoded colors — semantic tokens only
- ✓ Reuses shared components (no duplication)
- ✓ Loading (skeleton), empty and error states present
- ✓ No layout shift on load
- ✓ Error messages are clear and actionable (PT-PT)

**When all checks pass, it's impeccable.**
