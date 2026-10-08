import { useForm, useWatch } from 'react-hook-form';
import { toast } from 'sonner';
import { z } from 'zod';

import {
  useResetBrandingColorsMutation,
  useUpdateBrandingMutation,
} from '@/features/trainer-settings/api/settings';
import { HEX_COLOR, settingsErrorMessage } from '@/features/trainer-settings/lib/settings';
import { isApiProblem } from '@/shared/api/problem';
import type { components } from '@/shared/api/schema';
import { FormField } from '@/shared/components/FormField';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { BRAND_SURFACES, resolveBrand } from '@/shared/lib/brandTheme';

type Settings = components['schemas']['TrainerSettingsResponse'];

const COLOR = 'Usa uma cor no formato #RRGGBB ou deixa vazio para a cor do tema.';

/** Regras de `UpdateBrandingCommandValidator`: nome 2–50 (sem espaços nas pontas), cores #RRGGBB. */
const brandingSchema = z.object({
  app_name: z
    .string()
    .trim()
    .min(2, 'O nome da app tem de ter entre 2 e 50 caracteres.')
    .max(50, 'O nome da app tem de ter entre 2 e 50 caracteres.'),
  primary_color: z
    .string()
    .trim()
    .refine((value) => value === '' || HEX_COLOR.test(value), COLOR),
  body_color: z
    .string()
    .trim()
    .refine((value) => value === '' || HEX_COLOR.test(value), COLOR),
});
type BrandingValues = z.input<typeof brandingSchema>;

/** Campos do servidor (PascalCase do comando) para os campos do formulário. */
const SERVER_FIELDS: Readonly<Record<string, keyof BrandingValues>> = {
  AppName: 'app_name',
  PrimaryColor: 'primary_color',
  BodyColor: 'body_color',
};

/**
 * Marca: nome da app e cores, com pré-visualização local.
 *
 * As cores só se aplicam a sério no portal do cliente; aqui a
 * pré-visualização mostra o efeito sem mexer no tema da app do personal trainer. Cor vazia
 * envia `null`, que o servidor trata como "cor do tema".
 *
 * @param settings Definições atuais.
 */
export function BrandingSection({ settings }: { settings: Settings }) {
  const update = useUpdateBrandingMutation();
  const reset = useResetBrandingColorsMutation();

  // Rascunho inicializado uma vez: um refetch (foco, outra secção guardada) não apaga o que
  // está a ser escrito. Depois de guardar, repõe-se com a resposta do servidor.
  const form = useForm<BrandingValues>({ defaultValues: toValues(settings) });
  const errors = form.formState.errors;
  const preview = useWatch({ control: form.control });
  const hasCustomColors = settings.primary_color !== null || settings.body_color !== null;

  async function submit(values: BrandingValues) {
    const parsed = brandingSchema.safeParse(values);
    if (!parsed.success) {
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === 'string' && field in values)
          form.setError(field as keyof BrandingValues, { message: issue.message });
      }
      return;
    }

    const data = parsed.data;
    try {
      const saved = await update.mutateAsync({
        app_name: data.app_name,
        primary_color: data.primary_color === '' ? null : data.primary_color,
        body_color: data.body_color === '' ? null : data.body_color,
      });
      form.reset(toValues(saved));
      toast.success('Marca guardada com sucesso.');
    } catch (error) {
      const message = settingsErrorMessage(error, 'Não foi possível guardar a marca.');
      const field = mappedField(error);
      form.setError(field ?? 'root', { message });
    }
  }

  async function resetColors() {
    try {
      await reset.mutateAsync();
      // Só as cores: o nome que esteja escrito mantém-se.
      form.reset({ ...form.getValues(), primary_color: '', body_color: '' });
      toast.success('Cores do tema repostas.');
    } catch (error) {
      toast.error(settingsErrorMessage(error, 'Não foi possível repor as cores do tema.'));
    }
  }

  return (
    <section
      aria-labelledby="branding-title"
      className="border-border space-y-4 rounded-xl border p-5"
    >
      <div>
        <h2 id="branding-title" className="font-display text-xl">
          Marca
        </h2>
        <p className="text-muted-foreground text-sm">
          Nome e cores que os teus clientes veem no portal.
        </p>
      </div>
      <form
        noValidate
        onSubmit={form.handleSubmit((values) => void submit(values))}
        className="grid gap-6 lg:grid-cols-[1fr_16rem]"
      >
        <div className="space-y-4">
          <FormField label="Nome da app" error={errors.app_name?.message}>
            {(control) => <Input {...control} {...form.register('app_name')} />}
          </FormField>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Cor principal" error={errors.primary_color?.message}>
              {(control) => (
                <Input {...control} placeholder="#RRGGBB" {...form.register('primary_color')} />
              )}
            </FormField>
            <FormField label="Cor de fundo" error={errors.body_color?.message}>
              {(control) => (
                <Input {...control} placeholder="Cor do tema" {...form.register('body_color')} />
              )}
            </FormField>
          </div>
          {errors.root && (
            <p role="alert" className="text-destructive text-sm">
              {errors.root.message}
            </p>
          )}
          <div className="flex flex-wrap gap-2">
            <Button type="submit" disabled={update.isPending}>
              {update.isPending ? 'A guardar…' : 'Guardar marca'}
            </Button>
            {hasCustomColors && (
              <Button
                type="button"
                variant="outline"
                disabled={reset.isPending}
                onClick={() => void resetColors()}
              >
                {reset.isPending ? 'A repor…' : 'Repor cores do tema'}
              </Button>
            )}
          </div>
        </div>
        <BrandPreview
          appName={preview.app_name ?? ''}
          primaryColor={preview.primary_color ?? ''}
          bodyColor={preview.body_color ?? ''}
          logoUrl={settings.logo_url}
        />
      </form>
    </section>
  );
}

function toValues(settings: Settings): BrandingValues {
  return {
    app_name: settings.app_name,
    primary_color: settings.primary_color ?? '',
    body_color: settings.body_color ?? '',
  };
}

/** Campo do formulário que corresponde ao primeiro erro de campo do servidor. */
function mappedField(error: unknown): keyof BrandingValues | undefined {
  if (!isApiProblem(error)) return undefined;
  return error.fieldErrors
    .map((item) => SERVER_FIELDS[item.field])
    .find((field) => field !== undefined);
}

/**
 * Pré-visualização do portal nos dois temas, com o mesmo algoritmo que o portal usa
 * (`resolveBrand`): a cor principal sai com o tom ajustado ao AA e a cor de fundo pinta só
 * o cabeçalho, com texto preto ou branco. Assim o personal trainer vê o que o cliente vai ver, e
 * porque é que um laranja claro aparece mais escuro no tema claro.
 *
 * Só chega ao `style` uma cor que passou em `isHexColor`; texto livre nunca.
 */
function BrandPreview({
  appName,
  primaryColor,
  bodyColor,
  logoUrl,
}: {
  appName: string;
  primaryColor: string;
  bodyColor: string;
  logoUrl: string | null;
}) {
  const branding = {
    primary_color: primaryColor.trim() === '' ? null : primaryColor.trim(),
    body_color: bodyColor.trim() === '' ? null : bodyColor.trim(),
  };
  const name = appName.trim() === '' ? 'Nome da app' : appName.trim();

  return (
    <figure className="space-y-2">
      <figcaption className="text-muted-foreground text-xs">Pré-visualização do portal</figcaption>
      {(['light', 'dark'] as const).map((theme) => {
        const surface = BRAND_SURFACES[theme];
        const brand = resolveBrand(branding, theme);
        return (
          <div
            key={theme}
            data-testid={`brand-preview-${theme}`}
            className="border-border overflow-hidden rounded-xl border"
            style={{ backgroundColor: surface.card, color: surface.foreground }}
          >
            <div
              data-testid={`brand-preview-header-${theme}`}
              className="flex items-center gap-2 border-b border-current/10 px-3 py-2"
              style={
                brand.header === null
                  ? undefined
                  : { backgroundColor: brand.header.background, color: brand.header.foreground }
              }
            >
              {logoUrl !== null && (
                <img src={logoUrl} alt="" className="size-8 rounded object-contain" />
              )}
              <span className="font-display truncate text-lg">{name}</span>
            </div>
            <div className="flex items-center justify-between gap-2 p-3">
              <span className="text-xs">{theme === 'light' ? 'Tema claro' : 'Tema escuro'}</span>
              <span
                className="inline-flex rounded-md px-3 py-1.5 text-sm"
                style={{
                  backgroundColor: brand.primary?.background ?? surface.primary,
                  color: brand.primary?.foreground ?? surface.primaryForeground,
                }}
              >
                Treino de hoje
              </span>
            </div>
          </div>
        );
      })}
    </figure>
  );
}
