import axios, {
  AxiosError,
  AxiosInstance,
  InternalAxiosRequestConfig,
} from 'axios';
import { API_BASE_URL, API_TIMEOUT_MS, STORAGE_KEYS } from '../constants/config';
import * as SecureStore from 'expo-secure-store';

// ── Logout callback ───────────────────────────────────────────────────────────
// AuthContext registers this on mount so the interceptor can navigate to Login
// without importing React context here (avoids circular deps).

type LogoutHandler = () => void;
let _logoutHandler: LogoutHandler | null = null;

export function setLogoutHandler(handler: LogoutHandler) {
  _logoutHandler = handler;
}

// ── Token refresh state ───────────────────────────────────────────────────────
// Prevents concurrent 401 responses from all firing refresh simultaneously.

let isRefreshing = false;
let refreshSubscribers: Array<(token: string) => void> = [];

function subscribeTokenRefresh(cb: (token: string) => void) {
  refreshSubscribers.push(cb);
}

function notifySubscribers(token: string) {
  refreshSubscribers.forEach(cb => cb(token));
  refreshSubscribers = [];
}

// ── Axios instance ─────────────────────────────────────────────────────────────

export const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  timeout: API_TIMEOUT_MS,
  headers: { 'Content-Type': 'application/json' },
});

// ── Request interceptor: attach access token ──────────────────────────────────

apiClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    const token = await SecureStore.getItemAsync(STORAGE_KEYS.ACCESS_TOKEN);
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  error => Promise.reject(error),
);

// ── Response interceptor: silent token refresh on 401 ─────────────────────────

apiClient.interceptors.response.use(
  response => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & {
      _retry?: boolean;
    };

    // Only attempt refresh on 401 and if we haven't retried yet
    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error);
    }

    originalRequest._retry = true;

    if (isRefreshing) {
      // Another request is already refreshing — queue this one until done
      return new Promise<string>((resolve) => {
        subscribeTokenRefresh(resolve);
      }).then((newToken) => {
        if (originalRequest.headers) {
          originalRequest.headers.Authorization = `Bearer ${newToken}`;
        }
        return apiClient(originalRequest);
      });
    }

    isRefreshing = true;

    try {
      const refreshToken = await SecureStore.getItemAsync(STORAGE_KEYS.REFRESH_TOKEN);
      if (!refreshToken) throw new Error('No refresh token stored');

      // Use a plain axios call (not apiClient) to avoid interceptor recursion
      const { data } = await axios.post(
        `${API_BASE_URL}/api/auth/refresh`,
        { refreshToken },
        { headers: { 'Content-Type': 'application/json' } },
      );

      const newAccess: string  = data.accessToken;
      const newRefresh: string = data.refreshToken;

      await SecureStore.setItemAsync(STORAGE_KEYS.ACCESS_TOKEN,  newAccess);
      await SecureStore.setItemAsync(STORAGE_KEYS.REFRESH_TOKEN, newRefresh);

      notifySubscribers(newAccess);

      if (originalRequest.headers) {
        originalRequest.headers.Authorization = `Bearer ${newAccess}`;
      }
      return apiClient(originalRequest);
    } catch (refreshError) {
      refreshSubscribers = [];
      // Refresh failed → clear tokens and force logout
      await SecureStore.deleteItemAsync(STORAGE_KEYS.ACCESS_TOKEN);
      await SecureStore.deleteItemAsync(STORAGE_KEYS.REFRESH_TOKEN);
      _logoutHandler?.();
      return Promise.reject(refreshError);
    } finally {
      isRefreshing = false;
    }
  },
);
