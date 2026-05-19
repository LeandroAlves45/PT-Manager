/**
 * App.jsx — componente raiz e definição de todas as rotas.
 *
 * Arquitectura de 3 dashboards:
 *   /login              → LoginPage (pública)
 *   /admin/*            → Layout de superuser (a construir na Fase 2)
 *   /trainer/*          → Layout de Personal Trainer (actual conteúdo da app)
 *   /cliente/*          → Portal do cliente (a construir na Fase 3)
 *
 * Cada namespace de rotas tem o seu próprio ProtectedRoute com o role exigido.
 * O conteúdo de cada dashboard tem o seu próprio layout (sidebar, navegação).
 *
 * Rotas sem autenticação:
 *   /login              → página de login
 *   /                   → redireciona para /login (nunca deve ficar em /)
 */

import { Routes, Route, Navigate } from 'react-router-dom';
import ProtectedRoute from './components/ProtectedRoute';

// Páginas públicas
import LoginPage from './pages/LoginPage';
import TrainerSignupPage from './pages/TrainerSignupPage';

// Layouts
import TrainerLayout from './layouts/TrainerLayout';
import AdminLayout from './layouts/AdminLayout';
import ClientLayout from './layouts/ClientLayout';

// Páginas do Personal Trainer
import Dashboard from './pages/Dashboard';
import Clients from './pages/Clients';
import ClientDetails from './pages/ClientDetails';
import Sessions from './pages/Sessions';
import Packs from './pages/Packs';
import Exercises from './pages/Exercises';
import TrainingPlans from './pages/TrainingPlans';
import TrainingPlanEditorPage from './pages/trainer/TrainingPlanEditorPage';
import BillingPage from './pages/trainer/BillingPage';
import TrainerProfilePage from './pages/trainer/TrainerProfilePage';
import SupplementsPage from './pages/trainer/SupplementPage';
import NutritionPage from './pages/trainer/NutritionPage';
import AssessmentsPage from './pages/trainer/AssessmentPage';
import MealPlanPage from './pages/trainer/MealsPlanPage';
import ClientCalculatorPage from './pages/trainer/ClientCalculatePage';

// Páginas de Admin
import AdminDashboard from './pages/admin/AdminDashboard';
import AdminTrainers from './pages/admin/AdminTrainers';
import AdminExercises from './pages/admin/AdminExercises';
import AdminFoods from './pages/admin/AdminFoods';
import AdminSupplements from './pages/admin/AdminSupplements';

// Páginas de fluxo público (sem autenticação)
import ClientFirstLoginPage from './pages/ClientFirstLoginPage';

// Páginas do Cliente
import ClientDashboard from './pages/client/ClientDashboard';
import MyTrainingPlan from './pages/client/MyTrainingPlan';
import MyNutrition from './pages/client/MyNutrition';
import MyCheckIns from './pages/client/MyCheckins';
import MyProfile from './pages/client/MyProfile';

// Componente raiz da aplicação.

export default function App() {
  return (
    <Routes>
      {/* ============================================================
                ROTAS PÚBLICAS — não requerem autenticação
            ============================================================ */}

      {/* Página de login — acessível a todos */}
      <Route path="/login" element={<LoginPage />} />

      {/* Página de registo de Personal Trainers — acessível a todos */}
      <Route path="/signup" element={<TrainerSignupPage />} />

      {/* Raiz redireciona para login — nunca deve ficar em / */}
      <Route path="/" element={<Navigate to="/login" replace />} />

      {/* Página de primeiro login do cliente via link de convite */}
      <Route path="/invite/:token" element={<ClientFirstLoginPage />} />

      {/* ============================================================
                ROTAS DO TRAINER 
            ============================================================ */}

      <Route
        path="/trainer/*"
        element={
          <ProtectedRoute requiredRole="trainer">
            <TrainerLayout />
          </ProtectedRoute>
        }
      >
        {/* Redireciona /trainer para /trainer/dashboard */}
        <Route index element={<Navigate to="dashboard" replace />} />

        {/* Dashboard principal */}
        <Route path="dashboard" element={<Dashboard />} />

        {/* Gestão de clientes */}
        <Route path="clientes" element={<Clients />} />
        <Route path="clientes/:id" element={<ClientDetails />} />

        {/* Calculadora de macros por cliente */}
        <Route
          path="clientes/:id/calculadora"
          element={<ClientCalculatorPage />}
        />

        {/* Gestão de sessões */}
        <Route path="sessoes" element={<Sessions />} />

        {/* Gestão de packs */}
        <Route path="packs" element={<Packs />} />

        {/* Gestão de exercícios */}
        <Route path="exercicios" element={<Exercises />} />

        {/* Gestão de planos de treino */}
        <Route path="planos" element={<TrainingPlans />} />

        {/* Editor de plano de treino */}
        <Route path="planos/:id" element={<TrainingPlanEditorPage />} />

        {/* Página de faturação e subscrição */}
        <Route path="billing" element={<BillingPage />} />

        {/* Página de perfil do Personal Trainer */}
        <Route path="perfil" element={<TrainerProfilePage />} />

        {/* Página de nutrição */}
        <Route path="nutricao" element={<NutritionPage />} />

        {/* Página de avaliação */}
        <Route path="avaliacoes" element={<AssessmentsPage />} />

        {/* Catálogo de suplementos do Personal Trainer */}
        <Route path="suplementos" element={<SupplementsPage />} />

        {/* Página de planos alimentares (NR-01) */}
        <Route path="planos-alimentares" element={<MealPlanPage />} />
      </Route>

      {/* ============================================================
                ROTAS DO SUPERUSER — requerem role="superuser"
            ============================================================ */}

      <Route
        path="/admin/*"
        element={
          <ProtectedRoute requiredRole="superuser">
            <AdminLayout />
          </ProtectedRoute>
        }
      >
        {/* Redireciona /admin para /admin/dashboard */}
        <Route index element={<Navigate to="dashboard" replace />} />

        {/* Dashboard de administração */}
        <Route path="dashboard" element={<AdminDashboard />} />

        {/* Gestão de Personal Trainers */}
        <Route path="trainers" element={<AdminTrainers />} />

        {/* Catálogos globais */}
        <Route path="exercicios" element={<AdminExercises />} />
        <Route path="alimentos" element={<AdminFoods />} />
        <Route path="suplementos" element={<AdminSupplements />} />
      </Route>

      {/* ============================================================
                ROTAS DO CLIENTE — requerem role="client"
            ============================================================ */}

      <Route
        path="/cliente/*"
        element={
          <ProtectedRoute requiredRole="client">
            <ClientLayout />
          </ProtectedRoute>
        }
      >
        {/* Redireciona /cliente para /cliente/dashboard */}
        <Route index element={<Navigate to="dashboard" replace />} />

        {/* Dashboard principal do cliente */}
        <Route path="dashboard" element={<ClientDashboard />} />

        {/* Plano de treino do cliente */}
        <Route path="plano" element={<MyTrainingPlan />} />

        {/* Plano de nutrição do cliente */}
        <Route path="nutricao" element={<MyNutrition />} />

        {/* Check-ins do cliente */}
        <Route path="checkins" element={<MyCheckIns />} />

        {/* Perfil do cliente */}
        <Route path="perfil" element={<MyProfile />} />

        {/* Suplementação foi integrada em Nutrição — manter redirect para compatibilidade */}
        <Route path="suplementos" element={<Navigate to="/cliente/nutricao" replace />} />
      </Route>

      {/* Fallback para rotas desconhecidas — redireciona para login */}
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}
