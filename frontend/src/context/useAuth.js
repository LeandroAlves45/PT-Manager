import { useContext } from 'react';
import { AuthContext } from './AuthContextStore';

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error(
      'useAuth deve ser usado dentro de <AuthProvider>. Verifica o main.jsx.'
    );
  }

  return context;
}
