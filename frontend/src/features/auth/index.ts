/**
 * API pública da feature auth.
 *
 * As páginas são usadas pelo router; o resto (campo de palavra-passe, regras, mapa de erros,
 * hook do Google) é reutilizado por "Conta e segurança" (feature account), que não pode
 * importar ficheiros internos desta feature.
 */
export { AcceptInvitePage } from '@/features/auth/pages/AcceptInvitePage';
export { AcceptTrainerInvitePage } from '@/features/auth/pages/AcceptTrainerInvitePage';
export { ConfirmEmailPage } from '@/features/auth/pages/ConfirmEmailPage';
export { ForgotPasswordPage } from '@/features/auth/pages/ForgotPasswordPage';
export { LoginPage } from '@/features/auth/pages/LoginPage';
export { ResendConfirmationPage } from '@/features/auth/pages/ResendConfirmationPage';
export { ResetPasswordPage } from '@/features/auth/pages/ResetPasswordPage';
export { SignUpPage } from '@/features/auth/pages/SignUpPage';

export { PasswordField } from '@/features/auth/components/PasswordField';
export { useGoogleIdentity } from '@/features/auth/hooks/useGoogleIdentity';
export { authErrorMessage } from '@/features/auth/lib/auth-errors';
export {
  peekPendingAuthNotice,
  consumePendingAuthNotice,
  resetPendingAuthNotice,
  setPendingAuthNotice,
} from '@/features/auth/lib/auth-notice';
export { applyProblemToForm } from '@/features/auth/lib/form-errors';
export { endLocalSession } from '@/features/auth/lib/local-session';
export {
  PASSWORD_RULE_HINT,
  changePasswordSchema,
  type ChangePasswordFormValues,
} from '@/features/auth/schemas/password.schema';
