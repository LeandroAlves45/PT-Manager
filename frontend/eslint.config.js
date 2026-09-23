import js from '@eslint/js';
import vitest from '@vitest/eslint-plugin';
import prettier from 'eslint-config-prettier';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

/** Configuração de ESLint (flat config).*/
export default tseslint.config(
  {
    ignores: [
      'dist',
      'coverage',
      'node_modules',
      'src/shared/api/schema.d.ts',
      'public/mockServiceWorker.js',
    ],
  },

  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommendedTypeChecked],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],

      // O contrato da API é snake_case; converter para camelCase criaria uma camada de
      // mapeamento só para agradar ao lint. A regra dica desligada.
      '@typescript-eslint/naming-convention': 'off',

      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-floating-promises': 'error',
      '@typescript-eslint/no-misused-promises': [
        'error',
        { checksVoidReturn: { attributes: false } },
      ],
      'no-restricted-globals': [
        'error',
        {
          name: 'localStorage',
          message: 'Tokens e dados da sessão vivem só em memória.',
        },
        {
          name: 'sessionStorage',
          message: 'Tokens e dados da sessão vivem só em memória.',
        },
      ],
    },
  },
  {
    // Os componentes do shadcn/ui exportam variantes (`buttonVariants`) a par do
    // componente. É o formato oficial da biblioteca e não vale a pena dividir ficheiros
    // vendorizados só para calar um aviso de fast refresh.
    files: ['src/shared/components/ui/**/*.tsx'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },

  {
    files: ['src/**/*.{test,spec}.{ts,tsx}', 'src/test/**/*.{ts,tsx}'],
    plugins: { vitest },
    rules: {
      ...vitest.configs.recommended.rules,
      '@typescript-eslint/no-non-null-assertion': 'off',
    },
  },

  {
    files: ['vite.config.ts', 'eslint.config.js', 'scripts/**/*.mjs'],
    languageOptions: { globals: globals.node },
    extends: [tseslint.configs.disableTypeChecked],
  },

  prettier
);
