import axios from 'axios';
import { API_BASE_URL, API_TIMEOUT_MS } from '../constants/config';
import { LoginRequest, TokenResponse, RefreshRequest } from '../types/api';

// Auth calls use a plain axios instance (no Bearer interceptor needed for login/refresh)
const authAxios = axios.create({
  baseURL: API_BASE_URL,
  timeout: API_TIMEOUT_MS,
  headers: { 'Content-Type': 'application/json' },
});

export const authApi = {
  login: async (body: LoginRequest): Promise<TokenResponse> => {
    const { data } = await authAxios.post<TokenResponse>('/api/auth/login', body);
    return data;
  },

  refresh: async (body: RefreshRequest): Promise<TokenResponse> => {
    const { data } = await authAxios.post<TokenResponse>('/api/auth/refresh', body);
    return data;
  },

  logout: async (accessToken: string, refreshToken: string): Promise<void> => {
    // Best-effort — ignore failures (token will expire anyway)
    try {
      await authAxios.post(
        '/api/auth/logout',
        { refreshToken },
        { headers: { Authorization: `Bearer ${accessToken}` } },
      );
    } catch {
      // ignore
    }
  },
};
