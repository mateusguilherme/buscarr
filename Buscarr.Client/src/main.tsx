import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Route, Routes } from 'react-router';
import { DownloadsPage } from './DownloadsPage';
import { HomePage } from './HomePage';
import { locale } from './i18n';
import { ListPage } from './ListPage';
import { SettingsPage } from './SettingsPage';
import './index.css';

document.documentElement.lang = locale;

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/list" element={<ListPage />} />
        <Route path="/downloads" element={<DownloadsPage />} />
        <Route path="/settings" element={<SettingsPage />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
);
