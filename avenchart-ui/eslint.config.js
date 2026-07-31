// SPDX-FileCopyrightText: 2026 Neil Kimber and Legacy EHR Modernization Project contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
    },
    rules: {
      // These React Compiler-oriented diagnostics are not applicable until the
      // project enables the compiler. Core Rules of Hooks checks remain active.
      'react-hooks/immutability': 'off',
      'react-hooks/set-state-in-effect': 'off',
      // Route modules and the toast module intentionally export testable helpers
      // alongside their components.
      'react-refresh/only-export-components': 'off',
    },
  },
])
