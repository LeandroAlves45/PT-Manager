/**
 * MyProfile.jsx - perfil do cliente autenticado.
 *
 * Esta página mostra apenas os dados da conta em modo de leitura e a ação
 * de alteração de password. O cliente não edita aqui o resto do perfil.
 */

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { toast } from 'react-toastify';
import {
  UserRound,
  ShieldCheck,
  LockKeyhole,
  Eye,
  EyeOff,
  Save,
} from 'lucide-react';
import { getMyProfile, changeMyPassword } from '@/api/clientPortalApi';
import { confirmPasswordRules, passwordRules } from '@/utils/validators';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

function formatProfileValue(value) {
  if (value === null || value === undefined || value === '') {
    return 'Não definido';
  }

  return value;
}

function formatSexLabel(value) {
  const labels = {
    male: 'Masculino',
    female: 'Feminino',
    other: 'Outro',
    unknown: 'Não definido',
  };

  return labels[value] ?? formatProfileValue(value);
}

function formatTrainingModality(value) {
  const labels = {
    presencial: 'Presencial',
    online: 'Online',
  };

  return labels[value] ?? formatProfileValue(value);
}

function InfoRow({ label, value }) {
  return (
    <div className="flex flex-col gap-1 rounded-lg border border-border bg-background px-4 py-3">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {label}
      </p>
      <p className="text-sm text-foreground">{formatProfileValue(value)}</p>
    </div>
  );
}

function PasswordField({
  id,
  label,
  placeholder,
  register,
  rules,
  error,
  visible,
  onToggle,
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <div className="relative">
        <Input
          id={id}
          type={visible ? 'text' : 'password'}
          placeholder={placeholder}
          className="pr-10"
          {...register(id, rules)}
        />
        <button
          type="button"
          onClick={onToggle}
          className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
        >
          {visible ? (
            <EyeOff className="h-4 w-4" />
          ) : (
            <Eye className="h-4 w-4" />
          )}
        </button>
      </div>
      {error && <p className="text-xs text-destructive">{error.message}</p>}
    </div>
  );
}

export default function MyProfile() {
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [savingPassword, setSavingPassword] = useState(false);
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  const {
    register,
    handleSubmit,
    watch,
    reset,
    formState: { errors },
  } = useForm({
    defaultValues: {
      current_password: '',
      new_password: '',
      confirm_password: '',
    },
  });

  const newPasswordValue = watch('new_password');

  useEffect(() => {
    getMyProfile()
      .then((data) => setProfile(data))
      .catch((error) => {
        toast.error(
          error?.response?.data?.detail ?? 'Erro ao carregar perfil do cliente'
        );
      })
      .finally(() => setLoading(false));
  }, []);

  async function onSubmitPassword(data) {
    setSavingPassword(true);

    try {
      const response = await changeMyPassword({
        current_password: data.current_password,
        new_password: data.new_password,
      });

      toast.success(response?.message ?? 'Password atualizada com sucesso.');
      reset();
    } catch (error) {
      const detail = error?.response?.data?.detail;
      const message =
        Array.isArray(detail) && detail.length > 0
          ? detail[0]?.msg
          : (detail ?? 'Erro ao atualizar password.');

      toast.error(message);
    } finally {
      setSavingPassword(false);
    }
  }

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center min-h-64">
        <div className="h-6 w-6 rounded-full border-2 border-primary border-t-transparent animate-spin" />
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="p-4 lg:p-6">
        <Card className="border-border bg-card">
          <CardContent className="p-8 flex flex-col items-center gap-3 text-center">
            <UserRound className="h-10 w-10 text-muted-foreground" />
            <p className="font-medium text-foreground">Perfil indisponível</p>
            <p className="text-sm text-muted-foreground">
              Não foi possível carregar os teus dados neste momento.
            </p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="p-4 lg:p-6">
      <div className="max-w-5xl flex flex-col gap-6">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Perfil</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Consulta os teus dados da conta e altera a tua password quando
            precisares.
          </p>
        </div>

        <Card className="border-border bg-card">
          <CardHeader className="pb-3">
            <div className="flex items-start justify-between gap-3 flex-wrap">
              <div className="flex items-center gap-3">
                <div className="h-11 w-11 rounded-full bg-primary/10 flex items-center justify-center">
                  <UserRound className="h-5 w-5 text-primary" />
                </div>
                <div>
                  <CardTitle className="text-lg">{profile.full_name}</CardTitle>
                  <p className="text-sm text-muted-foreground">
                    {formatProfileValue(profile.email)}
                  </p>
                </div>
              </div>
              <Badge variant="secondary">
                {formatTrainingModality(profile.training_modality)}
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <InfoRow label="Nome completo" value={profile.full_name} />
            <InfoRow label="Email" value={profile.email} />
            <InfoRow label="Telefone" value={profile.phone} />
            <InfoRow label="Data de nascimento" value={profile.birth_date} />
            <InfoRow label="Sexo" value={formatSexLabel(profile.sex)} />
            <InfoRow
              label="Altura"
              value={
                profile.height_cm !== null && profile.height_cm !== undefined
                  ? `${profile.height_cm} cm`
                  : null
              }
            />
          </CardContent>
        </Card>

        <Card className="border-border bg-card">
          <CardHeader className="pb-3">
            <div className="flex items-center gap-2">
              <LockKeyhole className="h-4 w-4 text-primary" />
              <CardTitle className="text-base">Segurança da conta</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="rounded-lg border border-border bg-background px-4 py-3">
              <div className="flex items-start gap-3">
                <div className="mt-0.5 h-8 w-8 rounded-lg bg-primary/10 flex items-center justify-center shrink-0">
                  <ShieldCheck className="h-4 w-4 text-primary" />
                </div>
                <div>
                  <p className="text-sm font-medium text-foreground">
                    Alterar password
                  </p>
                  <p className="text-xs text-muted-foreground mt-1">
                    Para tua segurança, precisas de introduzir a password atual
                    antes de definires uma nova.
                  </p>
                </div>
              </div>
            </div>

            <form
              onSubmit={handleSubmit(onSubmitPassword)}
              className="grid grid-cols-1 xl:grid-cols-2 gap-4"
            >
              <PasswordField
                id="current_password"
                label="Password atual"
                placeholder="Introduz a tua password atual"
                register={register}
                rules={{ required: 'A password atual é obrigatória' }}
                error={errors.current_password}
                visible={showCurrentPassword}
                onToggle={() => setShowCurrentPassword((prev) => !prev)}
              />

              <div className="xl:hidden" />

              <PasswordField
                id="new_password"
                label="Nova password"
                placeholder="Mínimo de 8 caracteres"
                register={register}
                rules={passwordRules}
                error={errors.new_password}
                visible={showNewPassword}
                onToggle={() => setShowNewPassword((prev) => !prev)}
              />

              <PasswordField
                id="confirm_password"
                label="Confirmar nova password"
                placeholder="Repete a nova password"
                register={register}
                rules={confirmPasswordRules(newPasswordValue)}
                error={errors.confirm_password}
                visible={showConfirmPassword}
                onToggle={() => setShowConfirmPassword((prev) => !prev)}
              />

              <div className="xl:col-span-2 flex justify-end">
                <Button type="submit" disabled={savingPassword}>
                  <Save className="h-4 w-4 mr-2" />
                  {savingPassword ? 'A guardar...' : 'Atualizar password'}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
