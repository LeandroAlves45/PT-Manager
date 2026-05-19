/**
 * main.jsx — ponto de entrada da aplicação React.
 *
 * Ordem dos providers (de fora para dentro):
 *   BrowserRouter → necessário para o AuthContext usar useNavigate()
 *   AuthProvider  → disponibiliza user, login, logout a toda a app
 *   App           → define as rotas e os layouts
 *   ToastContainer → renderiza os toasts (react-toastify) por cima de tudo
 */

import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';
import './index.css';
import App from './App.jsx';
import { AuthProvider } from './context/AuthContext';

/**
 * Entry point da aplicação.
 *
 * - BrowserRouter: Fornece o sistema de routing (react-router-dom)
 *   Envolve toda a app para que qualquer componente possa usar
 *   useNavigate, useLocation, Link, etc.
 *
 * - ToastContainer: Renderiza as notificações toast (react-toastify)
 *   Configurado com tema dark para combinar com o design.
 *   Qualquer componente pode chamar toast.success(), toast.error(), etc.
 *
 * - StrictMode: Ativa verificações adicionais em desenvolvimento
 *   (renderiza componentes duas vezes para detetar side-effects)
 */
ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
        <ToastContainer
          position="bottom-right"
          autoClose={3000}
          hideProgressBar={false}
          newestOnTop
          closeOnClick
          pauseOnFocusLoss
          draggable
          pauseOnHover
          theme="dark"
        />
      </AuthProvider>
    </BrowserRouter>
  </React.StrictMode>
);
