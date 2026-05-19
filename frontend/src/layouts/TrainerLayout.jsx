/**
 * TrainerLayout.jsx — layout do dashboard do Personal Trainer.
 *
 * Envolve todas as rotas /trainer/* com:
 *   - A Sidebar de navegação (desktop fixa, mobile hamburger)
 *   - A área principal de conteúdo com o padding correcto
 *
 * O <Outlet /> é onde o react-router-dom renderiza a página activa.
 * Exemplo: /trainer/clientes → Outlet renderiza <Clients />
 */

import { Outlet } from 'react-router-dom';
import Sidebar from '@/components/Sidebar';

export default function TrainerLayout() {
  return (
    <div className="min-h-screen bg-background text-foreground">
      {/* Sidebar — fixa no desktop, Sheet no mobile */}
      <Sidebar />

      {/* Área principal de conteúdo */}
      <main className="lg:pl-64 min-h-screen">
        <Outlet />
      </main>
    </div>
  );
}
