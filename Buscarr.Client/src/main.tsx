import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Route, Routes } from 'react-router';
import { HomePage } from './HomePage';
import { locale } from './i18n';
import { ReleasesPage } from './ReleasesPage';
import './index.css';

document.documentElement.lang = locale;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/artist/:id" element={<ReleasesPage />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
);
