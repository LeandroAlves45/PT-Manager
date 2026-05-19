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
  useState,
  useEffect,
  useCallback,
} from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import { getPortalBranding } from '../api/clientPortalApi';
import { hexToHSL } from '../utils/formatters';
import { AuthContext } from './AuthContextStore';

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

  // Fundo da sidebar — mesmo hue da cor primária mas muito escuro (6% lightness).
  // Isto dá o efeito de "sidebar colorida" sem comprometer a legibilidade.
  // Ex: azul #00A8E8 → hue ≈ 197, sidebar fica "197 100% 6%" (azul muito escuro)
  const huePart = hsl.split(' ')[0]; // extrai apenas o hue (ex: "197")
  const satPart = hsl.split(' ')[1]; // extrai saturação (ex: "100%")
  document.documentElement.style.setProperty(
    '--sidebar-background',
    `${huePart} ${satPart} 6%`
  );
}

/**
 * applyBodyColor — aplica a cor de fundo do body (--background, --card, --popover).
 * Independente da cor primária — permite body cinzento com sidebar azul, por exemplo.
 * Chamada no login, restore de sessão e ao guardar settings.
 * Se hexColor for null/vazio, repõe os valores padrão do tema.
 */
function applyBodyColor(hexColor) {
  if (!hexColor) {
    // Repor os valores padrão do tema dark
    document.documentElement.style.removeProperty('--background');
    document.documentElement.style.removeProperty('--card');
    document.documentElement.style.removeProperty('--popover');
    return;
  }
  const hsl = hexToHSL(hexColor);
  document.documentElement.style.setProperty('--background', hsl);
  // Card e popover ligeiramente mais claros (+ 4% lightness)
  const parts = hsl.split(' ');
  const lightness = Math.min(parseFloat(parts[2]) + 4, 95);
  const cardHsl = `${parts[0]} ${parts[1]} ${lightness}%`;
  document.documentElement.style.setProperty('--card', cardHsl);
  document.documentElement.style.setProperty('--popover', cardHsl);
}

// ============================================================
// Provider — envolve a app e disponibiliza o contexto
// ============================================================

export function AuthProvider({ children }) {
  // Utilizador autenticado: { id, email, full_name, role, client_id } | null
  const [user, setUser] = useState(null);

  // Token JWT guardado no localStorage — lido aqui para sincronizar o estado
  const [token, setToken] = useState(() => localStorage.getItem('pt_token'));

  // Settings de branding do Personal Trainer: { primary_color, logo_url, app_name } | null
  const [trainerSettings, setTrainerSettings] = useState(null);

  // true enquanto a app verifica se há um token guardado válido (splash inicial)
  const [isLoading, setIsLoading] = useState(true);

  const navigate = useNavigate();

  // ----------------------------------------------------------
  // Aplicar um payload de branding no estado e nas CSS variables
  // ----------------------------------------------------------
  const applyBrandingSettings = useCallback((settings) => {
    const normalizedSettings = settings
      ? {
          primary_color: settings.primary_color ?? settings.primaryColor ?? null,
          body_color: settings.body_color ?? settings.bodyColor ?? null,
          logo_url: settings.logo_url ?? settings.logoUrl ?? null,
          app_name: settings.app_name ?? settings.appName ?? 'PT Manager',
        }
      : null;

    setTrainerSettings(normalizedSettings);

    if (normalizedSettings?.primary_color) {
      applyBrandColor(normalizedSettings.primary_color);
    } else {
      applyBrandColor('#00A8E8'); // cor por defeito se não houver settings
    }

    // body_color: aplica sempre (null repõe cor padrão do tema)
    applyBodyColor(normalizedSettings?.body_color ?? null);
  }, []);

  // ----------------------------------------------------------
  // Carregar settings do Personal Trainer
  // Chamado após login com role "trainer" ou no restore de sessão
  // ----------------------------------------------------------

  const fetchTrainerSettings = useCallback(async () => {
    try {
      // GET /api/v1/trainer-profile/settings — devolve primary_color, logo_url, app_name
      const response = await api.get('/api/v1/trainer-profile/settings');
      const settings = response.data;
      applyBrandingSettings(settings);
    } catch {
      applyBrandingSettings(null); // se não existirem settings, usa as cores por defeito do tema
    }
  }, [applyBrandingSettings]);

  // ----------------------------------------------------------
  // Carregar branding herdado no portal do cliente
  // ----------------------------------------------------------
  const fetchClientBranding = useCallback(async () => {
    try {
      const settings = await getPortalBranding();
      applyBrandingSettings(settings);
    } catch {
      applyBrandingSettings(null); // se falhar, usa as cores por defeito do tema
    }
  }, [applyBrandingSettings]);

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
        // Sem token — utilizador não está autenticado
        setIsLoading(false);
        return;
      }

      try {
        // Valida o token fazendo um pedido autenticado ao perfil
        // O interceptor do Axios injeta o Bearer token automaticamente
        const response = await api.get('/api/v1/auth/users/me');
        const userData = response.data;

        setUser(userData);
        setToken(storedToken);

      // Carrega branding conforme o role autenticado
      if (userData.role === 'trainer') {
        await fetchTrainerSettings();
      } else if (userData.role === 'client') {
        await fetchClientBranding();
      }
      } catch {
        // Token inválido ou expirado — limpa o estado
        // O interceptor 401 do Axios já redireciona para /login
        localStorage.removeItem('pt_token');
        setToken(null);
        setUser(null);
      } finally {
        setIsLoading(false);
      }
    };

    restoreSession();
  }, [fetchTrainerSettings, fetchClientBranding]);

  // ----------------------------------------------------------
  // Login
  //
  // 1. Chama POST /api/v1/auth/login com email e password
  // 2. Guarda o token JWT no localStorage
  // 3. Busca o perfil completo do utilizador
  // 4. Carrega branding do Personal Trainer
  // 5. Redireciona para o dashboard correcto baseado no role
  // ----------------------------------------------------------

  const login = useCallback(
    async (email, password) => {
      // Chama o endpoint de login — devolve { access_token, role, user_id, full_name }
      const response = await api.post('/api/v1/auth/login', {
        email,
        password,
      });
      const { access_token, role } = response.data;

      // Guarda token no localStorage para persistência entre sessões
      localStorage.setItem('pt_token', access_token);
      setToken(access_token);

      // Busca o perfil completo (o login só devolve dados básicos do JWT)
      const profileResponse = await api.get('/api/v1/auth/users/me');
      const userData = profileResponse.data;
      setUser(userData);

      // Carrega e aplica branding antes de redirecionar (evita flash de cor errada)
      if (role === 'trainer') {
        await fetchTrainerSettings();
      } else if (role === 'client') {
        await fetchClientBranding();
      }

      // Redireciona para o dashboard correcto baseado no role do JWT
      // Cada role tem o seu próprio layout e conjunto de rotas
      if (role === 'superuser') {
        navigate('/admin/dashboard');
      } else if (role === 'trainer') {
        navigate('/trainer/dashboard');
      } else if (role === 'client') {
        navigate('/cliente/dashboard');
      }
    },
    [navigate, fetchTrainerSettings, fetchClientBranding]
  );

  // ----------------------------------------------------------
  // Logout
  //
  // Limpa todo o estado de autenticação e redireciona para login.
  // ----------------------------------------------------------

  const logout = useCallback(() => {
    // Remove token do localStorage — o interceptor Axios deixa de o injectar
    localStorage.removeItem('pt_token');
    setToken(null);
    setUser(null);
    setTrainerSettings(null);

    // Repõe a cor por defeito (PT Manager azul) ao fazer logout
    applyBrandColor('#00A8E8');
    applyBodyColor(null); // repõe cor de fundo padrão do tema

    // Redireciona para a página de login

    navigate('/login');
  }, [navigate]);

  // ----------------------------------------------------------
  // Valores expostos pelo contexto
  // ----------------------------------------------------------

  const value = {
    user,
    token,
    trainerSettings,
    isLoading,

    // Funções
    login,
    logout,
    fetchTrainerSettings, // exposto para o TrainerProfile atualizar o branding em runtime
    fetchClientBranding,
    applyBrandColor, // exposto para permitir atualizar a cor sem recarregar settings

    // Booleans de conveniência — evitam comparações de string nos componentes
    isAuthenticated: !!user,
    isTrainer: user?.role === 'trainer',
    isClient: user?.role === 'client',
    isSuperuser: user?.role === 'superuser',
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
