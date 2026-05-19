/**
 * ClientFirstLoginPage.jsx — página de primeiro login do cliente via link de convite.
 *
 * Rota: /invite/:token (pública — o cliente ainda não tem sessão)
 *
 * Fluxo:
 *   1. Ao montar, valida o token via GET /invite/validate/:token
 *      → se inválido/expirado, mostra mensagem de erro
 *      → se válido, mostra o nome do cliente e o formulário de password
 *   2. Cliente submete a nova password
 *      → chama POST /invite/set-password/:token
 *      → recebe JWT automaticamente
 *   3. Guarda o JWT no localStorage (mesmo padrão do AuthContext)
 *   4. Redireciona para /cliente/dashboard
 *
 * Nota: esta página não usa AuthContext.login() porque o cliente
 * não tem email/password ainda — o JWT vem directamente da resposta do set-password.
 */

import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import {
  Dumbbell,
  Eye,
  EyeOff,
  CheckCircle,
  AlertCircle,
  Loader2,
} from 'lucide-react';
import { validateInvite, setPasswordViaInvite } from '@/api/inviteApi';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

export default function ClientFirstLoginPage() {
  // Extrai o raw token do URL
  const { token } = useParams();
  const navigate = useNavigate();

  // Estados para validação do token
  const [Validating, setValidating] = useState(true);
  const [TokenValid, setTokenValid] = useState(false);
  const [clientName, setClientName] = useState('');
  const [ErroMessage, setErroMessage] = useState('');

  // Estado aós definição de password
  const [sucess, setSucess] = useState(false);

  // Visibilidade da password
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      newPassword: '',
      confirmPassword: '',
    },
  });

  // Valor da password para a regra de confirmação
  const passwordValue = watch('newPassword');

  // Valida o token ao montar a página
  useEffect(() => {
    if (!token) {
      setTokenValid(false);
      setErroMessage('Link de convite inválido.');
      setValidating(false);
      return;
    }

    validateInvite(token)
      .then((data) => {
        if (data.valid) {
          setTokenValid(true);
          setClientName(data.client_name);
        } else {
          setTokenValid(false);
          setErroMessage(
            data.message || 'Link de convite inválido ou expirado.'
          );
        }
      })
      .catch(() => {
        setTokenValid(false);
        setErroMessage('Erro ao validar o link de convite.');
      })
      .finally(() => setValidating(false));
  }, [token]);

  // Submeter nova password
  const onSubmit = async (data) => {
    try {
      const result = await setPasswordViaInvite(token, data.newPassword);
      // Guarda o JWT no localStorage
      localStorage.setItem('pt_token', result.access_token);

      setSucess(true);

      // Redireciona para o dashboard do cliente após um breve delay
      setTimeout(() => navigate('/cliente/dashboard'), 1500);
    } catch {
      setErroMessage('Erro ao definir a password. Tente novamente.');
      setTokenValid(false);
    }
  };

  // Render: a carregar
  if (Validating) {
    return (
      <div className="min-h-screen flex-items-center justify-center bg-background">
        <div className="flex flex-col items-center gap-3 text-muted-foreground">
          <Loader2 className="h-8 w-8 animate-spin" />
          <p className="text-sm">A verificar o link de convite...</p>
        </div>
      </div>
    );
  }

  // Render: token inválido ou erro
  if (!TokenValid && !sucess) {
    return (
      <div className="min-h-screen flex-items-center justify-center bg-background p-4">
        <Card className="w-full max-w-md">
          <CardHeader className="text-center">
            <div className="flex justify-center mb-4">
              <div className="rounded-full bg-destructive/10 p-3">
                <AlertCircle className="h-8 w-8 text-destructive" />
              </div>
            </div>
            <CardTitle>Link Inválido</CardTitle>
            <CardDescription>{ErroMessage}</CardDescription>
          </CardHeader>
          <CardContent className="text-center">
            <p className="text-sm text-muted-foreground mb-4">
              Pede ao teu Personal Trainer para gerar um novo link de convite.
            </p>
            <Button variant="outline" onClick={() => navigate('/login')}>
              Ir para o Login
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Render: sucesso
  if (sucess) {
    return (
      <div className="min-h-screen flex-items-center justify-center bg-background p-4">
        <Card className="w-full max-w-md">
          <CardHeader className="text-center">
            <div className="flex justify-center mb-4">
              <div className="rounded-full bg-green-500/10 p-3">
                <CheckCircle className="h-8 w-8 text-green-500" />
              </div>
            </div>
            <CardTitle>Bem-vindo, {clientName}!</CardTitle>
            <CardDescription>
              A tua password foi definida com sucesso. A redirecionar...
            </CardDescription>
          </CardHeader>
        </Card>
      </div>
    );
  }

  // Render: formulário de definição de password
  return (
    <div className="min-h-screen flex-items-center justify-center bg-background p-4">
      <div className="w-full max-w-md space-y-6">
        {/* Logótipo PT Manager */}
        <div className="flex flex-col items-center gap-2">
          <div className="rounded-full bg-primary/10 p-3">
            <Dumbbell className="h-8 w-8 text-primary" />
          </div>
          <h1 className="text-2xl font-bold text-foreground">PT Manager</h1>
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Bem-vindo/a, {clientName}!</CardTitle>
            <CardDescription>
              O teu Personal Trainer convidou-te para a plataforma. Define a tua
              password para ativares a tua conta.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Campo nova password */}
            <div className="space-y-1">
              <Label htmlFor="newPassword">Nova Password</Label>
              <div className="relative">
                <Input
                  id="newPassword"
                  type={showPassword ? 'text' : 'password'}
                  placeholder="Mínimo 6 caracteres"
                  {...register('newPassword', {
                    required: 'A password é obrigatória.',
                    minLength: {
                      value: 6,
                      message: 'A password deve ter pelo menos 6 caracteres.',
                    },
                  })}
                />
                <button
                  type="button"
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                  onClick={() => setShowPassword((prev) => !prev)}
                >
                  {showPassword ? (
                    <EyeOff className="h-4 w-4" />
                  ) : (
                    <Eye className="h-4 w-4" />
                  )}
                </button>
              </div>
              {errors.newPassword && (
                <p className="text-xs text-destructive">
                  {errors.newPassword.message}
                </p>
              )}
            </div>

            {/* Campo confirmar password */}
            <div className="space-y-1">
              <Label htmlFor="confirm_Password">Confirmar Password</Label>
              <div className="relative">
                <Input
                  id="confirm_Password"
                  type={showConfirm ? 'text' : 'password'}
                  placeholder="Reintroduz a password"
                  {...register('confirmPassword', {
                    required: 'Confirmação de password é obrigatória.',
                    validate: (value) =>
                      value === passwordValue || 'As passwords não coincidem.',
                  })}
                />
                <button
                  type="button"
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                  onClick={() => setShowConfirm((prev) => !prev)}
                >
                  {showConfirm ? (
                    <EyeOff className="h-4 w-4" />
                  ) : (
                    <Eye className="h-4 w-4" />
                  )}
                </button>
              </div>
              {errors.confirmPassword && (
                <p className="text-xs text-destructive">
                  {errors.confirmPassword.message}
                </p>
              )}
            </div>

            {/* Botão de submissão */}
            <Button
              className="w-full"
              onClick={handleSubmit(onSubmit)}
              disabled={isSubmitting}
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="animate-spin mr-2 h-4 w-4" />A ativar
                  conta...
                </>
              ) : (
                'Ativar Conta'
              )}
            </Button>
          </CardContent>
        </Card>

        {/* link para login */}
        <p className="text-center text-sm text-muted-foreground">
          Já tens conta?{' '}
          <button
            type="button"
            className="text-primary hover:underline font-medium"
            onClick={() => navigate('/login')}
          >
            Ir para o Login
          </button>
        </p>
      </div>
    </div>
  );
}
