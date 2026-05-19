/**
 * ProtectedRoute.jsx — guarda de rotas baseado em autenticação e role.
 *
 * Uso no App.jsx:
 *   <Route path="/trainer/*" element={<ProtectedRoute requiredRole="trainer"><TrainerLayout /></ProtectedRoute>} />
 *
 * Comportamento:
 *   1. Enquanto o AuthContext está a verificar o token guardado → mostra loading
 *   2. Utilizador não autenticado → redireciona para /login
 *   3. Utilizador autenticado mas com role errado → redireciona para o seu dashboard correcto
 *   4. Tudo correcto → renderiza os filhos (children)
 */

import { Navigate } from 'react-router-dom';
import { useAuth } from '@/context/useAuth';

/**
 * Dado um role, devolve o caminho do dashboard correcto.
 * Usado para redirecionar um utilizador que tentou aceder
 * a uma rota que não corresponde ao seu role.
 */
function getDashboardForRole(role) {
  if (role === 'superuser') return '/admin/dashboard';
  if (role === 'trainer') return '/trainer/dashboard';
  if (role === 'client') return '/client/dashboard';
  return '/login'; // Fallback para roles desconhecidos
}

/**
 * @param {React.ReactNode} children      — conteúdo a renderizar se o acesso for permitido
 * @param {string}          requiredRole  — role necessário: 'trainer' | 'client' | 'superuser'
 *                                         se omitido, apenas verifica autenticação
 */

export default function ProtectedRoute({ children, requiredRole }) {
  const { isAuthenticated, user, isLoading } = useAuth();

  // AuthContext ainda está a verificar o token do localStorage.
  // Mostramos um ecrã de loading em vez de redirecionar prematuramente
  if (isLoading) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          {/* Spinner simples com cor primária do tema */}
          <div className="h-8 w-8 rounded-full border-2 border-primary border-t-transparent animate-spin" />
          <p className="text-sm text-muted-foreground">A carregar...</p>
        </div>
      </div>
    );
  }

  // Sem sessão activa → redireciona para login.
  // replace={true} evita que o /login fique no histórico de navegação
  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace />;
  }

  if (requiredRole && user.role !== requiredRole) {
    // Superuser pode aceder a rotas de Personal Trainer, tem acesso total
    const isSuperuserAccessingTrainerRoute =
      user.role === 'superuser' && requiredRole === 'trainer';

    if (!isSuperuserAccessingTrainerRoute) {
      return <Navigate to={getDashboardForRole(user.role)} replace />;
    }
  }

  // Tudo correcto → renderiza o conteudo protegido
  return children;
}
