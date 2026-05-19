/**
 * AuthContext.jsx — contexto global de autenticação.
 *
 * Responsabilidades:
 *   - Armazenar o utilizador autenticado e o seu token JWT
 *   - Executar o login (chamar a API, guardar token, carregar branding)
 *   - Executar o logout (limpar estado e redirecionar)
 *   - Injectar o tema de cor do Personal Trainer via CSS variables no documento
 *   - Expor booleans de role para simplificar guards nos componentes
 *
 * Padrão de uso:
 *   // Qualquer componente dentro de <AuthProvider> pode fazer:
 *   const { user, login, logout, isTrainer, trainerSettings } = useAuth();
 *
 * O contexto é providenciado em main.jsx — envolve toda a app.
 */

import {
  createContext,
  useState,
  useEffect,
  useContext,
  useCallback,
} from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';

// ============================================================
// Criação do contexto de autenticação - valor por defeito é null
// ============================================================

const AuthContext = createContext(null);

// ============================================================
// Função utilitária: converte cor hex para HSL sem a função hsl()
//
// O sistema de tema Tailwind v4 usa CSS variables no formato "H S% L%"
// (sem "hsl()" à volta) para permitir que as utilities como bg-primary
// sejam definidas como hsl(var(--primary) / <alpha>).
//
// Exemplo: "#00A8E8" → "201 100% 45%"
// ============================================================

function hexToHSL(hex) {
  // Remove o # se existir e converte para números RGB
  const clean = hex.replace('#', '');
  const r = parseInt(clean.substring(0, 2), 16) / 255;
  const g = parseInt(clean.substring(2, 4), 16) / 255;
  const b = parseInt(clean.substring(4, 6), 16) / 255;

  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  let h, s;
  const l = (max + min) / 2;

  if (max === min) {
    // Cinzento - sem matiz
    h = s = 0;
  } else {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

    switch (max) {
      case r:
        h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
        break;
      case g:
        h = ((b - r) / d + 2) / 6;
        break;
      case b:
        h = ((r - g) / d + 4) / 6;
        break;
    }
  }

  // Formato esperado pelo Tailwind v4: "201 100% 45%"
  return `${Math.round(h * 360)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%`;
}

// ============================================================
// Função utilitária: aplica a cor do Personal Trainer como CSS variables
//
// Ao mudar --primary em runtime, todos os componentes que usam
// bg-primary, text-primary, border-primary actualizam automaticamente
// sem necessidade de rebuild — é o núcleo do white-label branding.
// ============================================================

function applyBrandColor(hexColor) {
  if (!hexColor) return;

  const hsl = hexToHSL(hexColor);

  // Aplica a todas as CSS variables que derivam da cor primária
  document.documentElement.style.setProperty('--primary', hsl);
  document.documentElement.style.setProperty('--sidebar-primary', hsl);
  document.documentElement.style.setProperty('--ring', hsl);
}

// ============================================================
// Provider - envolve a app e disponibiliza o contexto de autenticação
// ============================================================

export function AuthProvider({ children }) {
  // Utilizador autenticado: { id, full_name, role, client_id } | null
  const [user, setUser] = useState(null);

  // Token JWT guardado no localStorage para persistência entre sessões
  const [token, setToken] = useState(() => localStorage.getItem('pt_token'));

  // Settings de branding do Personal Trainer: { primary_color, logo_url, app_name } ou null
  const [trainerSettings, setTrainerSettings] = useState(null);

  // true enquanto a app verifica se há token guardado válido
  const [isLoading, setIsLoading] = useState(true);

  const navigate = useNavigate();

  // ============================================================
  // Carregar Settings do Trainer
  // Chamado após login com role "trainer" para obter as cores e logo personalizados
  // ============================================================

  const fetchTrainerSettings = useCallback(async () => {
    try {
      // GET /api/v1/trainer-profile/settings — devolve primary_color, logo_url, app_name
      const response = await api.get('/api/v1/trainer-profile/settings');
      const settings = response.data;

      setTrainerSettings(settings);

      // Aplica a cor do Personal Trainer imediatamente após carregar
      if (settings?.primary_color) {
        applyBrandColor(settings.primary_color);
      }
    } catch {
      // Se não existirem settings (Personal Trainer novo), usa a cor por defeito
      applyBrandColor('#00A8E8');
    }
  }, []);

  // ----------------------------------------------------------
  // Restaurar sessão ao carregar a app
  //
  // Ao abrir a app, verifica se há token guardado no localStorage.
  // Se sim, busca o perfil do utilizador para validar que o token
  // ainda é válido e restaurar o estado.
  // ----------------------------------------------------------

  useEffect(() => {
    const restoreSession = async () => {
      const storedToken = localStorage.getItem('pt_token');

      if (!storedToken) {
        // Sem token - utilizador não autenticado
        setIsLoading(false);
        return;
      }

      try {
        // Valida o token buscando o perfil do utilizador
        // O Interceptor do Axios irá automaticamente incluir o token na Authorization header
        const response = await api.get('/api/v1/auth/users/me');
        const userData = response.data;

        setUser(userData);
        setToken(storedToken);

        // Carrega branding se for Personal Trainer
        if (userData.role === 'trainer') {
          await fetchTrainerSettings();
        }
      } catch {
        // Token inválido ou expirado - limpa o estado
        localStorage.removeItem('pt_token');
        setToken(null);
        setUser(null);
      } finally {
        setIsLoading(false);
      }
    };
    restoreSession();
  }, [fetchTrainerSettings]);

  // ============================================================
  // Login - chama a API, guarda token e perfil, carrega branding
  // ============================================================

  const login = useCallback(
    async (email, password) => {
      // Chama o endpoint de login - devolve { access_token, role, user_id, full_name}
      const response = await api.post('/api/v1/auth/login', {
        email,
        password,
      });
      const { access_token, role } = response.data;

      // Guarda o token no estado e localStorage para persistência
      localStorage.setItem('pt_token', access_token);
      setToken(access_token);

      // Busca o perfil do utilizador para obter os detalhes e validar o token
      const profileResponse = await api.get('/api/v1/auth/users/me');
      const userData = profileResponse.data;
      setUser(userData);

      // Carrega e aplica o branding se for Personal Trainer
      if (role === 'trainer') {
        await fetchTrainerSettings();
      }

      // Redireciona para a dashboard correta após login bem-sucedido
      if (role === 'superuser') {
        navigate('/admin/dashboard');
      } else if (role === 'trainer') {
        navigate('/trainer/dashboard');
      } else if (role === 'client') {
        navigate('/client/dashboard');
      }
    },
    [fetchTrainerSettings, navigate]
  );

  // ============================================================
  // Logout - limpa estado, localStorage e redireciona para login
  // ============================================================

  const logout = useCallback(() => {
    // Remove o token do localStorage e limpa o estado
    localStorage.removeItem('pt_token');
    setToken(null);
    setUser(null);
    setTrainerSettings(null);

    // Repões a cor para o tema por defeito
    applyBrandColor('#00A8E8');

    navigate('/login');
  }, [navigate]);

  // ============================================================
  // Valores expostos pelo contexto
  // ============================================================

  const value = {
    user,
    token,
    trainerSettings,
    isLoading,
    login,
    logout,
    fetchTrainerSettings,

    // Booleans de conveniência para guards nos componentes
    isAuthenticated: !!user,
    isSuperuser: user?.role === 'superuser',
    isTrainer: user?.role === 'trainer',
    isClient: user?.role === 'client',
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// ============================================================
// Hook de conveniência para consumir o contexto de autenticação
// ============================================================

export function useAuth() {
  const context = useContext(AuthContext);

  if (context === null) {
    throw new Error('useAuth deve ser usado dentro de um AuthProvider');
  }

  return context;
}
