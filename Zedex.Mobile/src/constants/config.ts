// ─────────────────────────────────────────────────────────────────────────────
// API Configuration
// ─────────────────────────────────────────────────────────────────────────────

// In EAS builds this is injected via eas.json → build.[profile].env.
// In local Expo Go dev, fall back to the hard-coded LAN IP.
//
// To change the server: update EXPO_PUBLIC_API_URL in eas.json and rebuild.
// To change the dev address: edit the fallback below.

export const API_BASE_URL: string =
  process.env.EXPO_PUBLIC_API_URL ?? 'http://192.168.1.12:83';

export const API_TIMEOUT_MS = 15_000;

export const STORAGE_KEYS = {
  ACCESS_TOKEN:      'zedex_access_token',
  REFRESH_TOKEN:     'zedex_refresh_token',
  BIOMETRIC_ENABLED: 'zedex_biometric_enabled',
} as const;

export const DEFAULT_PAGE_SIZE = 20;
