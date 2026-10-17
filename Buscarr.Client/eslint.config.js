import { defineConfig } from 'eslint/config';
import stylistic from '@stylistic/eslint-plugin';
import tseslint from 'typescript-eslint';

const blocks = ['if', 'for', 'while', 'do', 'try', 'switch'];

export default defineConfig({
  files: ['**/*.{ts,tsx}'],
  languageOptions: {
    parser: tseslint.parser,
  },
  plugins: {
    '@stylistic': stylistic,
  },
  rules: {
    '@stylistic/padding-line-between-statements': [
      'error',
      { blankLine: 'always', prev: '*', next: blocks },
      { blankLine: 'always', prev: blocks, next: '*' },
      { blankLine: 'always', prev: '*', next: 'return' },
    ],
  },
});
