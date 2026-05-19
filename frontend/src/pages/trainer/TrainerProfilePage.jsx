/**
 * TrainerProfilePage.jsx — perfil e branding do Personal Trainer.
 *
 * Funcionalidades:
 *   - Upload de logo (Cloudinary via backend)
 *   - Picker de cor primária com preview em tempo real
 *   - Nome da app personalizado
 *
 * Quando o Personal Trainer guarda a cor, é aplicada imediatamente via CSS variables
 * usando fetchTrainerSettings() do AuthContext — sem reload necessário.
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { hexColorRules } from '@/utils/validators';
import { toast } from 'react-toastify';
import { Upload, Trash2, Loader2, Palette } from 'lucide-react';
import { useAuth } from '../../context/useAuth';
import { hexToHSL } from '../../utils/formatters';
import api from '../../api/axiosConfig';
import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '../../components/ui/card';
import { Separator } from '@/components/ui/separator';

export default function TrainerProfilePage() {
  const { user, trainerSettings, fetchTrainerSettings, applyBrandColor } =
    useAuth();
  const [uploadLoading, setUploadLoading] = useState(false);
  const [deleteLoading, setDeleteLoading] = useState(false);
  const [settingsLoading, setSettingsLoading] = useState(false);
  // Preview local da cor — actualizado enquanto o utilizador mexe no input
  const [colorPreview, setColorPreview] = useState(
    trainerSettings?.primary_color ?? '#00A8E8'
  );
  // Preview da cor do body (background geral da app)
  // Default: cor escura neutra (#0A0A14) — o valor actual do CSS --background
  const [bodyColorPreview, setBodyColorPreview] = useState(
    trainerSettings?.body_color ?? '#0A0A14'
  );
  const fileInputRef = useRef(null);

  /*
   * Debounce de 300ms para o live preview de cor.
   *
   * Razão: applyBrandColor altera CSS variables em todo o DOM — é uma operação
   * de pintura que, se chamada em cada keystroke, causa jank visual.
   * Com debounce de 300ms, a cor actualiza-se enquanto o Personal Trainer arrasta o picker
   * ou escreve o hex, mas só após 300ms de pausa — imperceptível para o utilizador.
   *
   * useRef guarda o timer entre renders sem causar re-render quando muda.
   * useCallback estabiliza a função para não ser recriada em cada render.
   */
  const debounceTimerRef = useRef(null);
  const bodyColorDebounceRef = useRef(null);

  // Debounce para a cor primária — aplica via applyBrandColor (CSS variables)
  const handleColorChange = useCallback(
    (hexValue) => {
      setColorPreview(hexValue);
      if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = setTimeout(() => {
        if (/^#[0-9A-Fa-f]{6}$/.test(hexValue)) {
          applyBrandColor(hexValue);
        }
      }, 300);
    },
    [applyBrandColor]
  );

  // Debounce para a cor do body — altera directamente --background e --card
  // sem tocar na cor primária da sidebar.
  // hexToHex → HSL: convertemos para o formato que o Tailwind CSS v4 espera.
  const handleBodyColorChange = useCallback((hexValue) => {
    setBodyColorPreview(hexValue);
    if (bodyColorDebounceRef.current)
      clearTimeout(bodyColorDebounceRef.current);
    bodyColorDebounceRef.current = setTimeout(() => {
      if (/^#[0-9A-Fa-f]{6}$/.test(hexValue)) {
        // Converte hex para HSL (formato "H S% L%" sem hsl())
        const hsl = hexToHSL(hexValue);
        // --background: background geral da página
        document.documentElement.style.setProperty('--background', hsl);
        // --card: cards ligeiramente mais claros que o background
        // Aumentamos a lightness em ~4% para manter a hierarquia visual
        const parts = hsl.split(' ');
        const lightness = parseFloat(parts[2]) + 4;
        const cardHsl = `${parts[0]} ${parts[1]} ${Math.min(lightness, 95)}%`;
        document.documentElement.style.setProperty('--card', cardHsl);
        document.documentElement.style.setProperty('--popover', cardHsl);
      }
    }, 300);
  }, []);

  const {
    register,
    handleSubmit,
    formState: { errors },
    reset,
  } = useForm({
    defaultValues: {
      primary_color: trainerSettings?.primary_color ?? '#00A8E8',
      body_color: trainerSettings?.body_color ?? '#0A0A14',
      app_name: trainerSettings?.app_name ?? 'PT Manager',
    },
  });

  // Sincroniza o formulário quando as settings carregam do AuthContext
  useEffect(() => {
    if (trainerSettings) {
      reset({
        primary_color: trainerSettings.primary_color ?? '#00A8E8',
        body_color: trainerSettings.body_color ?? '#0A0A14',
        app_name: trainerSettings.app_name ?? 'PT Manager',
      });
      setColorPreview(trainerSettings.primary_color ?? '#00A8E8');
      setBodyColorPreview(trainerSettings.body_color ?? '#0A0A14');
    }
  }, [trainerSettings, reset]);

  // ---------------------------------------------------------------
  // Upload de logo
  // ---------------------------------------------------------------

  async function handleLogoUpload(event) {
    const file = event.target.files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);

    setUploadLoading(true);
    try {
      // O Content-type é definido automaticamente pelo browser como multipart/form-data
      await api.post('/api/v1/trainer-profile/logo', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      toast.success('Logo atualizado com sucesso!');
      // Recarrega as settings para mostrar o novo logo
      await fetchTrainerSettings();
    } catch (error) {
      toast.error(
        error?.response?.data?.detail ?? 'Erro ao fazer upload do logo.'
      );
    } finally {
      setUploadLoading(false);
      // Limpa o input para permitir re-upload do mesmo ficheiro se necessário
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }

  async function handleLogoDelete() {
    setDeleteLoading(true);
    try {
      await api.delete('/api/v1/trainer-profile/logo');
      toast.success('Logo removido com sucesso!');
      await fetchTrainerSettings();
    } catch (error) {
      toast.error(error?.response?.data?.detail ?? 'Erro ao remover o logo.');
    } finally {
      setDeleteLoading(false);
    }
  }

  // ---------------------------------------------------------------
  // Guardar configurações de branding
  // ---------------------------------------------------------------

  async function onSubmitSettings(data) {
    setSettingsLoading(true);
    try {
      await api.patch('/api/v1/trainer-profile/settings', {
        primary_color: data.primary_color,
        body_color: data.body_color || null,
        app_name: data.app_name,
      });
      toast.success('Configurações guardadas com sucesso!');
      // Recarrega as settings para atualizar o tema e outros componentes
      await fetchTrainerSettings();
    } catch (error) {
      toast.error(
        error?.response?.data?.detail ?? 'Erro ao guardar as configurações.'
      );
    } finally {
      setSettingsLoading(false);
    }
  }

  return (
    <div className="p-4 lg:p-6 flex flex-col gap-6 max-w-2xl">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">
          Perfil e Branding
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Personaliza a app com o teu logo e cores
        </p>
      </div>

      {/* Secção: Informação da conta (read-only) */}
      <Card className="border-border bg-card">
        <CardHeader>
          <CardTitle className="text-base">Informação da Conta</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div>
            <p className="text-sm text-muted-foreground mb-1">Nome</p>
            <p className="text-sm font-medium text-foreground">
              {user?.full_name}
            </p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground mb-1">Email</p>
            <p className="text-sm font-medium text-foreground">{user?.email}</p>
          </div>
        </CardContent>
      </Card>

      {/* Secção: Logo */}

      <Card className="border-border bg-card">
        <CardHeader>
          <CardTitle className="text-base">Logo</CardTitle>
          <CardDescription>
            Aparece na sidebar e nos emails de sessão enviados aos clientes.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {/* Preview do logo actual ou placeholder */}
          <div className="flex items-center gap-4">
            <div className="h-20 w-20 rounded-lg border border-border bg-muted/30 flex items-center justify-center overflow-hidden">
              {trainerSettings?.logo_url ? (
                <img
                  src={trainerSettings.logo_url}
                  alt="Logo"
                  className="h-full w-full object-cover p-2"
                />
              ) : (
                <p className="text-xs text-muted-foreground">Sem logo</p>
              )}
            </div>

            <div className="flex flex-col gap-2">
              {/* Input de ficheiro escondido - activado pelo botão abaixo */}
              <input
                ref={fileInputRef}
                type="file"
                accept="image/png, image/jpeg, image/jpg, image/webp"
                className="hidden"
                onChange={handleLogoUpload}
              />
              <Button
                variant="outline"
                size="sm"
                disabled={uploadLoading}
                onClick={() => fileInputRef.current?.click()}
              >
                {uploadLoading ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />A
                    carregar...
                  </>
                ) : (
                  <>
                    <Upload className="mr-2 h-4 w-4" /> Carregar logo
                  </>
                )}
              </Button>

              {trainerSettings?.logo_url && (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={deleteLoading}
                  onClick={handleLogoDelete}
                  className="text-destructive hover:bg-destructive"
                >
                  {deleteLoading ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />A
                      remover...
                    </>
                  ) : (
                    <>
                      <Trash2 className="mr-2 h-4 w-4" /> Remover logo
                    </>
                  )}
                </Button>
              )}
            </div>
          </div>
          <p className="text-sm text-muted-foreground">
            PNG, JPEG ou WEBP · Máximo 5MB · Recomendado: fundo transparente
          </p>
        </CardContent>
      </Card>

      {/* Secção: Cor e nome da app */}

      <Card className="border-border bg-card">
        <CardHeader>
          <CardTitle className="text-base">Tema</CardTitle>
          <CardDescription>
            A cor é aplicada em tempo real em toda a interface
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={handleSubmit(onSubmitSettings)}
            className="flex flex-col gap-4"
          >
            {/* Cor primária */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="primary_color">Cor primária</Label>
              <div className="flex items-center gap-3">
                {/* Input de cor nativo */}
                <Input
                  type="color"
                  value={colorPreview}
                  onChange={(e) => handleColorChange(e.target.value)}
                  className="h-10 w-14 rounded-md border border-border cursor-pointer bg-transparent p-1"
                />
                {/* Input de texto para valor hex manual */}
                <Input
                  id="primary_color"
                  placeholder="#00A8E8"
                  value={colorPreview}
                  className="font-mono uppercase max-w-32"
                  {...register('primary_color', {
                    ...hexColorRules,
                    onChange: (e) => handleColorChange(e.target.value),
                  })}
                />
                {/* Amostra de cor em tempo real */}
                <div
                  className="h-10 w-10 rounded-md border border-border shrink-0"
                  style={{ backgroundColor: colorPreview }}
                />
              </div>
              {errors.primary_color && (
                <p className="text-sm text-destructive">
                  {errors.primary_color.message}
                </p>
              )}
            </div>

            <Separator />

            {/* Cor do body (background geral da app) */}
            {/*
              A cor do body altera o background geral da página — independente
              da cor primária (botões, links, sidebar).
              Permite ao trainer ter uma sidebar azul com um body cinzento,
              por exemplo.
              O default é o tema escuro original (#0A0A14).
            */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="body_color">Cor do fundo (body)</Label>
              <div className="flex items-center gap-3">
                <Input
                  type="color"
                  value={bodyColorPreview}
                  onChange={(e) => handleBodyColorChange(e.target.value)}
                  className="h-10 w-14 rounded-md border border-border cursor-pointer bg-transparent p-1"
                />
                <Input
                  id="body_color"
                  placeholder="#0A0A14"
                  value={bodyColorPreview}
                  className="font-mono uppercase max-w-32"
                  {...register('body_color', {
                    pattern: {
                      value: /^#[0-9A-Fa-f]{6}$/,
                      message: 'Formato hex inválido (ex: #0A0A14)',
                    },
                    onChange: (e) => handleBodyColorChange(e.target.value),
                  })}
                />
                <div
                  className="h-10 w-10 rounded-md border border-border shrink-0"
                  style={{ backgroundColor: bodyColorPreview }}
                />
              </div>
              <p className="text-xs text-muted-foreground">
                Aplica-se ao fundo geral da app (não afecta a sidebar). Escuro
                recomendado para melhor contraste.
              </p>
              {errors.body_color && (
                <p className="text-sm text-destructive">
                  {errors.body_color.message}
                </p>
              )}
            </div>

            <Separator />

            {/* Nome da app */}
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="app_name">Nome da app</Label>
              <Input
                id="app_name"
                placeholder="PT Manager"
                {...register('app_name', {
                  required: 'O nome é obrigatório',
                  minLength: { value: 2, message: 'Mínimo 2 caracteres' },
                  maxLength: { value: 50, message: 'Máximo 50 caracteres' },
                })}
              />
              <p className="text-sm text-muted-foreground">
                Aparece na sidebar e no título do browser
              </p>
              {errors.app_name && (
                <p className="text-sm text-destructive">
                  {errors.app_name.message}
                </p>
              )}
            </div>

            <Button
              type="submit"
              disabled={settingsLoading}
              className="self-start"
            >
              {settingsLoading ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" /> A guardar...
                </>
              ) : (
                <>
                  <Palette className="mr-2 h-4 w-4" /> Guardar configurações
                </>
              )}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
