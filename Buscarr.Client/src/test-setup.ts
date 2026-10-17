import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

Object.defineProperty(navigator, 'languages', { value: ['pt-BR'], configurable: true });

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});
