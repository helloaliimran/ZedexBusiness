// ─────────────────────────────────────────────────────────────────────────────
// API Configuration
// ─────────────────────────────────────────────────────────────────────────────

// ⚠️ SET THIS to your PC's WiFi IP address.
// Find it: open Command Prompt → type `ipconfig` → look for "IPv4 Address"
// under your WiFi adapter. Example: 192.168.1.50
const PC_LAN_IP = '192.168.1.11'; // ← your PC's WiFi IP (run ipconfig to confirm)

// Use HTTP port (61814) — NOT the HTTPS port (61813).
// The phone won't trust your PC's dev SSL certificate.
const DEV_PORT = '61814';

export const API_BASE_URL = __DEV__
  ? `http://${PC_LAN_IP}:${DEV_PORT}`
  : 'https://your-production-api.com';

export const API_TIMEOUT_MS = 15_000;

export const STORAGE_KEYS = {
  ACCESS_TOKEN:      'zedex_access_token',
  REFRESH_TOKEN:     'zedex_refresh_token',
  BIOMETRIC_ENABLED: 'zedex_biometric_enabled',
} as const;

export const DEFAULT_PAGE_SIZE = 20;
