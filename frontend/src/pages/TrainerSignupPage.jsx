/**
 * TrainerSignupPage.jsx — página de registo público de novo Personal Trainer.
 *
 * Fluxo após submissão bem sucedida:
 *   1. API cria o utilizador + subscrição Stripe em trial
 *   2. Guarda o JWT devolvido e faz login automático
 *   3. Redireciona para /trainer/dashboard
 *
 * Página é pública — não requer autenticação.
 */

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { emailRules, trainerPasswordRules, fullNameRules } from '@/utils/validators';
import { toast } from 'react-toastify';
import { Dumbbell, Eye, EyeOff, Loader2 } from 'lucide-react';

import { signupTrainer } from '../api/authApi';
import { useAuth } from '../context/useAuth';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { Label } from '../components/ui/label';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '../components/ui/card';

export default function TrainerSignupPage() {
  const { login } = useAuth();
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    defaultValues: {
      full_name: '',
      email: '',
      password: '',
    },
  });

  async function onSubmit(data) {
    setIsLoading(true);
    try {
      // Regista o Personal Trainer (cria user + Stripe trial)
      await signupTrainer(data);

      // Login automático com as credenciais fornecidas
      await login(data.email, data.password);

      toast.success('Registo bem sucedido! Bem-vindo ao PT Manager.');
      // O AuthContext.login() já redireciona para /trainer/dashboard
    } catch (error) {
      const message =
        error?.response?.data?.detail ||
        ' Erro ao criar a conta. Verifique os dados e tente novamente.';
      toast.error(message);
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Cabeçalho */}
        <div className="flex flex-col items-center gap-2 mb-8">
          <div className="h-12 w-12 ronded-xl bg-primary/10 flex items-center justify-center">
            <Dumbbell className="h-6 w-6 text-primary" />
          </div>
          <h1 className="text-2xl font-bold text-foreground">PT Manager</h1>
          <p className="text-sm text-muted-foreground">
            Cria a tua conta — 15 dias grátis sem cartão
          </p>
        </div>

        <Card className="border-border bg-card">
          <CardHeader className="pb-4">
            <CardTitle className="text-lg">Criar conta</CardTitle>
            <CardDescription>
              Preenche os dados para começar o teu trial gratuito.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form
              onSubmit={handleSubmit(onSubmit)}
              className="flex flex-col gap-4"
            >
              {/* Nome completo */}
              <div className=" flex flex-col gap-1.5">
                <Label htmlFor="full_name">Nome completo</Label>
                <Input
                  id="full_name"
                  placeholder="Ex: Leandro Alves"
                  disabled={isLoading}
                  {...register('full_name', fullNameRules)}
                />
                {errors.full_name && (
                  <p className="text-sm text-destructive">
                    {errors.full_name.message}
                  </p>
                )}
              </div>

              {/* Email */}
              <div className=" flex flex-col gap-1.5">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  placeholder="leandro@example.com"
                  disabled={isLoading}
                  {...register('email', emailRules)}
                />
                {errors.email && (
                  <p className="text-sm text-destructive">
                    {errors.email.message}
                  </p>
                )}
              </div>

              {/* Password */}
              <div className=" flex flex-col gap-1.5">
                <Label htmlFor="password">Password</Label>
                <div className="relative">
                  <Input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    placeholder="Mínimo 8 caracteres"
                    autoComplete="new-password"
                    disabled={isLoading}
                    {...register('password', trainerPasswordRules)}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((prev) => !prev)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition-colors"
                    tabIndex={-1}
                  >
                    {showPassword ? (
                      <EyeOff className="h-4 w-4" />
                    ) : (
                      <Eye className="h-4 w-4" />
                    )}
                  </button>
                </div>
                {errors.password && (
                  <p className="text-sm text-destructive">
                    {errors.password.message}
                  </p>
                )}
              </div>

              <Button
                type="submit"
                disabled={isLoading}
                className="w-full mt-2"
              >
                {isLoading ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin mr-2" />A criar
                    conta...
                  </>
                ) : (
                  'Criar conta gratuita'
                )}
              </Button>
            </form>
          </CardContent>
        </Card>

        <p className="text-sm text-center text-muted-foreground mt-6">
          Já tens conta?{' '}
          <Link
            to="/login"
            className="text-primary hover:underline font-medium"
          >
            Inicia sessão
          </Link>
        </p>
      </div>
    </div>
  );
}
