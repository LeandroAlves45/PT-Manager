import js from '@eslint/js';
import vitest from '@vitest/eslint-plugin';
import prettier from 'eslint-config-prettier';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

/**
 * Regra de dependências entre camadas (Clean Architecture adaptada a uma página):
 *
 *   domain  ←  content / config / seo  ←  sections  ←  app
 *   ui (primitivas) não conhece nenhuma camada de negócio.
 *
 * Cada entrada proíbe os imports "para fora" da camada. Uma violação é erro de lint.
 */
const layer = (files, forbidden, why) => ({
  files,
  rules: {
    'no-restricted-imports': [
      'error',
      {
        patterns: forbidden.map((name) => ({ group: [`@/${name}`, `@/${name}/*`], message: why })),
      },
    ],
  },
});

/** Configuração de ESLint (flat config), alinhada com a do frontend. */
export default tseslint.config(
  { ignores: ['dist', 'dist-ssr', 'coverage', 'node_modules'] },

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
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-floating-promises': 'error',
      // Superfícies de XSS: o site não tem nenhuma razão legítima para as usar.
      'no-restricted-syntax': [
        'error',
        {
          selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']",
          message: 'Proibido: o conteúdo é renderizado pelo React, nunca como HTML cru.',
        },
        {
          selector: 'MemberExpression[property.name=/^(innerHTML|outerHTML)$/]',
          message: 'Proibido: usar a árvore do React em vez de HTML cru.',
        },
        {
          selector: "JSXAttribute[name.name='style']",
          message: "Proibido: a CSP (style-src 'self') bloqueia atributos style. Usar classes.",
        },
      ],
      'no-eval': 'error',
      'no-implied-eval': 'error',
    },
  },

  layer(
    ['src/domain/**'],
    ['content', 'config', 'seo', 'ui', 'sections', 'app', 'styles'],
    'domain é puro: não importa nenhuma outra camada.'
  ),
  layer(
    ['src/content/**', 'src/config/**'],
    ['seo', 'ui', 'sections', 'app'],
    'content/config só dependem de domain.'
  ),
  layer(
    ['src/seo/**'],
    ['content', 'ui', 'sections', 'app'],
    'seo recebe dados por parâmetro; só depende de domain/config.'
  ),
  layer(
    ['src/ui/**'],
    ['domain', 'content', 'config', 'seo', 'sections', 'app'],
    'ui são primitivas sem conhecimento de negócio.'
  ),
  layer(
    ['src/sections/**'],
    ['content', 'config', 'seo', 'app'],
    'secções recebem dados por props; a composição é do app/.'
  ),

  {
    // Variantes cva exportadas a par do componente (formato shadcn).
    files: ['src/ui/**/*.tsx', 'src/sections/brand-showcase.tsx'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },

  {
    files: ['src/test/**/*.{ts,tsx}'],
    plugins: { vitest },
    rules: {
      ...vitest.configs.recommended.rules,
      '@typescript-eslint/no-non-null-assertion': 'off',
      'no-restricted-imports': 'off',
    },
  },

  {
    files: ['vite.config.ts', 'eslint.config.js', 'scripts/**/*.mjs'],
    languageOptions: { globals: globals.node },
    extends: [tseslint.configs.disableTypeChecked],
  },

  prettier
);
