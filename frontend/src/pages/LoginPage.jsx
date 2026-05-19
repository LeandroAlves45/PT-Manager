/**
 * LoginPage.jsx — página de autenticação.
 *
 * Chama AuthContext.login() que trata de:
 *   - Chamar a API
 *   - Guardar o token
 *   - Carregar o branding do Personal Trainer
 *   - Redirecionar para o dashboard correcto
 *
 * Esta página não precisa de saber para onde redirecionar —
 * essa lógica está encapsulada no AuthContext.
 */

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { emailRules, passwordRules } from '@/utils/validators';
import { toast } from 'react-toastify';
import { Dumbbell, Eye, EyeOff, Loader2 } from 'lucide-react';

import { useAuth } from '@/context/useAuth';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/card';

const SLOW_REQUEST_THRESHOLD_MS = 5000; // 5 segundos

export default function LoginPage() {
  const { login } = useAuth();

  // Controla a visibilidade da password
  const [showPassword, setShowPassword] = useState(false);

  // Controla o estado de loading durante a chamada da API
  const [isLoading, setIsLoading] = useState(false);

  // Mostra após SLOW_REQUEST_THRESHOLD_MS para informar o utilizador que a requisição está a iniciar
  const [isSlowRequest, setIsSlowRequest] = useState(false);

  // React Hook Form para gerir o formulário de login
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({
    defaultValues: {
      email: '',
      password: '',
    },
  });

  async function onSubmit(data) {
    setIsLoading(true);
    setIsSlowRequest(false);

    // Timer: se o pedido demorar mais que 5s, avisa o utilizador que a requisição está a iniciar
    const slowTimer = setTimeout(() => {
      setIsSlowRequest(true);
    }, SLOW_REQUEST_THRESHOLD_MS);

    try {
      // AuthContext.login() trata de toda a lógica de autenticação e redirecionamento
      await login(data.email, data.password);
    } catch (error) {
      // O interceptor do Axios já trata do log
      const message =
        error.response?.data?.detail ||
        'Email ou password inválidos. Tente novamente.';
      toast.error(message);
    } finally {
      clearTimeout(slowTimer);
      setIsLoading(false);
      setIsSlowRequest(false);
    }
  }

  return (
    <div className="min-h-screen bg-background flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Cabeçalho com ícone — sem logo, cada Personal Trainer pode ter o seu próprio branding */}
        <div className="flex flex-col items-center gap-2 mb-6">
          <div className="h-12 w-12 rounded-xl bg-primary/10 flex items-center justify-center">
            <Dumbbell className="h-6 w-6 text-primary" />
          </div>
          <h1 className="text-2xl font-bold text-foreground">PT Manager</h1>
          <p className="text-sm text-muted-foreground">
            Inicia sessão na tua conta
          </p>
        </div>

        <Card className="border-border bg-card">
          <CardHeader className="pb-4">
            <CardTitle className="text-lg">Entrar</CardTitle>
            <CardDescription>
              Utiliza o email e password da tua conta
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form
              onSubmit={handleSubmit(onSubmit)}
              className="flex flex-col gap-4"
            >
              {/* Campo Email */}
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  placeholder="example@gmail.com"
                  autoComplete="email"
                  disabled={isLoading}
                  {...register('email', emailRules)}
                />
                {/* Mostra o erro de validação do email, se houver */}
                {errors.email && (
                  <p className="text-xs text-destructive">
                    {errors.email.message}
                  </p>
                )}
              </div>

              {/* Campo Password */}
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="password">Password</Label>
                <div className="relative">
                  <Input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    placeholder="********"
                    autoComplete="current-password"
                    disabled={isLoading}
                    className="pr-10" // Espaço para o ícone de mostrar/ocultar
                    {...register('password', passwordRules)}
                  />
                  {/* Ícone de mostrar/ocultar password */}
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
                {/* Mostra o erro de validação do password, se houver */}
                {errors.password && (
                  <p className="text-xs text-destructive">
                    {errors.password.message}
                  </p>
                )}
              </div>

              {/* Mensagem de requisição lenta */}
              {isSlowRequest && (
                <div className="rounded-md bg-primary/5 border border-primary/20 px-3 py-2">
                  <p className="text-xs text-muted-foreground text-center">
                    O servidor está a iniciar. Por favor aguarda uns segundos...
                  </p>
                </div>
              )}

              {/* Botão de submit */}
              <Button
                type="submit"
                className="w-full mt-2"
                disabled={isLoading}
              >
                {isLoading ? (
                  <>
                    {/* Spinner inline durante o loading */}
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                    {isSlowRequest ? 'A ligar ao servidor...' : 'A entrar...'}
                  </>
                ) : (
                  'Entrar'
                )}
              </Button>
            </form>
          </CardContent>
        </Card>

        {/* Rodapé — link para registo + link de suporte */}
        <div className="flex flex-col items-center gap-2 mt-6">
          <p className="text-sm text-muted-foreground">
            És Personal Trainer e ainda não tens conta?{' '}
            <Link
              to="/signup"
              className="text-primary font-medium hover:underline"
            >
              Regista-te aqui
            </Link>
          </p>
          <p className="text-xs text-muted-foreground">
            Problema a aceder?{' '}
            <span className="text-primary cursor-pointer hover:underline">
              Contacta o teu treinador
            </span>
          </p>
        </div>
      </div>
    </div>
  );
}
