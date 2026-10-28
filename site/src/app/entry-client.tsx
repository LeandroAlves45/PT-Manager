import { StrictMode } from 'react';
import { createRoot, hydrateRoot } from 'react-dom/client';

import { App } from '@/app/App';
import { resolvePagePath } from '@/app/pages';
import '@/styles/globals.css';

const container = document.getElementById('root');
if (!container) throw new Error('Elemento #root em falta no index.html');

// A mesma rota que o pré-render usou para este HTML; desconhecida cai na inicial.
const app = (
  <StrictMode>
    <App path={resolvePagePath(window.location.pathname) ?? '/'} />
  </StrictMode>
);

// Em produção o HTML vem pré-renderizado e é hidratado; no `vite dev` o root está vazio.
if (container.hasChildNodes()) hydrateRoot(container, app);
else createRoot(container).render(app);
